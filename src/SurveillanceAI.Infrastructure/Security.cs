using SurveillanceAI.Application.Localization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SurveillanceAI.Application;
using SurveillanceAI.Domain;

namespace SurveillanceAI.Infrastructure;

public sealed class DpapiProtector : ISecretProtector
{
    public string Protect(string value) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    public string Unprotect(string value) => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));
}
public sealed class Authentication(IDbContextFactory<AppDbContext> factory, Session session)
{
    private readonly PasswordHasher<User> hasher = new();
    private readonly SemaphoreSlim gate = new(1);
    public async Task<bool> NeedsSetupAsync() { await using var db = await factory.CreateDbContextAsync(); return !await db.Users.AnyAsync(); }
    public async Task SetupAsync(string username, string password)
    {
        await gate.WaitAsync();
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            if (await db.Users.AnyAsync()) throw new InvalidOperationException(Texts.T("Administrator setup is already complete."));
            ValidatePassword(password);
            var user = new User { Username = Normalize(username), Role = UserRole.Administrator };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user); db.Audit.Add(new AuditLog { UserId = user.Id, Action = "Initial administrator created" });
            await db.SaveChangesAsync();
        }
        finally { gate.Release(); }
    }
    public async Task LoginAsync(string username, string password)
    {
        await gate.WaitAsync();
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            var user = await db.Users.SingleOrDefaultAsync(x => x.Username == Normalize(username));
            // Perform the same expensive hash operation for unknown users to reduce account probing.
            if (user is null) { hasher.HashPassword(new User(), password); throw new UnauthorizedAccessException(Texts.T("Invalid credentials or account unavailable.")); }
            if (!user.Active || user.LockedUntilUtc > DateTime.UtcNow) throw new UnauthorizedAccessException(Texts.T("Invalid credentials or account unavailable."));
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
            {
                user.FailedAttempts++;
                if (user.FailedAttempts >= 5) { user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5); user.FailedAttempts = 0; }
                await db.SaveChangesAsync(); throw new UnauthorizedAccessException(Texts.T("Invalid credentials or account unavailable."));
            }
            if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, password);
            user.FailedAttempts = 0; user.LockedUntilUtc = null;
            db.Audit.Add(new AuditLog { UserId = user.Id, Action = "Login" }); await db.SaveChangesAsync();
            session.SignIn(user.Id, user.Role);
        }
        finally { gate.Release(); }
    }
    public void Logout() => session.Logout();
    public async Task ChangePasswordAsync(string current, string replacement)
    {
        session.Require(); ValidatePassword(replacement);
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.SingleAsync(x => x.Id == session.UserId);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed) throw new UnauthorizedAccessException(Texts.T("Current password is incorrect."));
        user.PasswordHash = hasher.HashPassword(user, replacement);
        db.Audit.Add(new AuditLog { UserId = user.Id, Action = "Password changed" }); await db.SaveChangesAsync();
    }
    public async Task<List<User>> UsersAsync() { session.Require(UserRole.Administrator); await using var db = await factory.CreateDbContextAsync(); return await db.Users.Select(x => new User { Id = x.Id, Username = x.Username, Role = x.Role, Active = x.Active }).ToListAsync(); }
    public async Task SaveUserAsync(Guid? id, string username, string? password, UserRole role, bool active)
    {
        session.Require(UserRole.Administrator);
        if (!Enum.IsDefined(role)) throw new ArgumentException(Texts.T("Select a supported user role."));
        await gate.WaitAsync();
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            var user = id is null ? new User() : await db.Users.SingleAsync(x => x.Id == id);
            if (user.Id == session.UserId && (!active || role != UserRole.Administrator)) throw new InvalidOperationException(Texts.T("You cannot deactivate or demote your current administrator account."));
            user.Username = Normalize(username); user.Role = role; user.Active = active;
            if (!string.IsNullOrEmpty(password)) { ValidatePassword(password); user.PasswordHash = hasher.HashPassword(user, password); }
            if (string.IsNullOrEmpty(user.PasswordHash)) throw new ArgumentException(Texts.T("A password is required for a new user."));
            if (id is null) db.Users.Add(user);
            db.Audit.Add(new AuditLog { UserId = session.UserId, Action = $"User updated: {user.Id}" }); await db.SaveChangesAsync();
        }
        finally { gate.Release(); }
    }
    private static string Normalize(string username) => string.IsNullOrWhiteSpace(username) || username.Length > 100 ? throw new ArgumentException(Texts.T("Enter a username of 1–100 characters.")) : username.Trim().ToUpperInvariant();
    private static void ValidatePassword(string password) { if (password.Length < 12 || password.Length > 256) throw new ArgumentException(Texts.T("Use a password between 12 and 256 characters.")); }
}
