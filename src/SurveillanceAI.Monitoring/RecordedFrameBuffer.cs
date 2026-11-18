using OpenCvSharp;

namespace SurveillanceAI.Monitoring;

public sealed record RecordedFrameLease(Guid CameraId, DateTime CapturedUtc, string Key, string Path, long Bytes);
public sealed record RecordedBufferSnapshot(long Frames, long Bytes, double OldestAgeSeconds, long RejectedFrames, string? Error);

// A durable JPEG spool. Atomic rename publishes a complete frame; leases survive crashes.
public sealed class RecordedFrameBuffer
{
    private readonly string root;
    private readonly long maxBytes;
    private readonly object sync = new();
    private readonly SortedDictionary<string, RecordedFrameLease> entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> leased = [];
    private long bytes, rejected;
    private string? error;

    public RecordedFrameBuffer(string directory, long maxBytes)
    {
        root = System.IO.Path.GetFullPath(directory); this.maxBytes = maxBytes;
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        Directory.CreateDirectory(root);
        foreach (var file in Directory.EnumerateFiles(root, "*.lease")) File.Move(file, System.IO.Path.ChangeExtension(file, ".frame"), true);
        foreach (var file in Directory.EnumerateFiles(root, "*.tmp")) File.Delete(file);
        foreach (var file in Directory.EnumerateFiles(root, "*.frame"))
        {
            var parts = System.IO.Path.GetFileNameWithoutExtension(file).Split('_');
            if (parts.Length != 3 || !long.TryParse(parts[0], out var ticks) || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || !Guid.TryParseExact(parts[1], "N", out var camera))
                throw new InvalidDataException("Invalid recorded frame filename.");
            var item = new RecordedFrameLease(camera, new DateTime(ticks, DateTimeKind.Utc), System.IO.Path.GetFileNameWithoutExtension(file), file, new FileInfo(file).Length);
            entries.Add(item.Key, item); bytes += item.Bytes;
        }
    }

    public RecordedBufferSnapshot Snapshot
    {
        get { lock (sync) return new(entries.Count, bytes, entries.Count == 0 ? 0 : Math.Max(0, (DateTime.UtcNow - entries.First().Value.CapturedUtc).TotalSeconds), rejected, error); }
    }

    public bool Append(Guid cameraId, Mat frame, DateTime capturedUtc)
    {
        Cv2.ImEncode(".jpg", frame, out var jpeg, [(int)ImwriteFlags.JpegQuality, 95]);
        lock (sync)
        {
            if (bytes + jpeg.Length > maxBytes || entries.Count >= 500_000)
            { rejected++; error = "Recorded buffer full"; return false; }
            if (new DriveInfo(System.IO.Path.GetPathRoot(root)!).AvailableFreeSpace < 512L * 1024 * 1024)
            { rejected++; error = "Insufficient free disk space"; return false; }
            var key = $"{capturedUtc.ToUniversalTime().Ticks:D19}_{cameraId:N}_{Guid.NewGuid():N}";
            var path = System.IO.Path.Combine(root, key + ".frame");
            try
            {
                using (var output = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(jpeg); output.Flush(true); }
                File.Move(path + ".tmp", path);
                var entry = new RecordedFrameLease(cameraId, capturedUtc.ToUniversalTime(), key, path, jpeg.Length);
                entries.Add(key, entry); bytes += jpeg.Length; error = null; return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                rejected++; error = exception.GetType().Name;
                if (File.Exists(path + ".tmp")) { try { File.Delete(path + ".tmp"); } catch (IOException) { } }
                return false;
            }
        }
    }

    public RecordedFrameLease? Claim(IReadOnlySet<Guid> cameras)
    {
        lock (sync)
        {
            var entry = entries.Values.FirstOrDefault(e => cameras.Contains(e.CameraId) && !leased.Contains(e.Key));
            if (entry is null) return null;
            var path = System.IO.Path.ChangeExtension(entry.Path, ".lease");
            File.Move(entry.Path, path); leased.Add(entry.Key);
            entry = entry with { Path = path }; entries[entry.Key] = entry; return entry;
        }
    }

    public bool IsFirstForCamera(RecordedFrameLease lease)
    { lock (sync) return entries.Values.First(e => e.CameraId == lease.CameraId).Key == lease.Key; }

    public void Complete(RecordedFrameLease lease)
    {
        lock (sync)
        {
            File.Delete(lease.Path); entries.Remove(lease.Key); leased.Remove(lease.Key); bytes -= lease.Bytes;
        }
    }

    public void Return(RecordedFrameLease lease)
    {
        lock (sync)
        {
            if (!leased.Remove(lease.Key)) return;
            var path = System.IO.Path.ChangeExtension(lease.Path, ".frame"); File.Move(lease.Path, path);
            entries[lease.Key] = lease with { Path = path };
        }
    }
}
