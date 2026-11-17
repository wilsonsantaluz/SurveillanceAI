[Português](../deployment.md) | English

# Windows deployment

Build on Windows x64 with .NET 10 and NuGet access during restore. Local validation used SDK 10.0.404. Exact package versions are pinned; EF/Extensions use .NET 10 servicing packages. SQLite's native bundle is pinned above the older vulnerable transitive dependency.

```powershell
dotnet build SurveillanceAI.sln -c Release
dotnet test SurveillanceAI.sln -c Release
dotnet publish src/SurveillanceAI.Desktop -c Release -r win-x64 --self-contained true -o artifacts/publish
./scripts/Smoke-Desktop.ps1
```

Launch artifacts/publish/SurveillanceAI.Desktop.exe. Keep the whole folder, including native libraries and models/legacy. This self-contained folder includes the desktop runtime; it is not a signed installer or single-file package. Validate target Windows/CPU, camera drivers/codecs, permissions and storage before a pilot.

For the local SDK only, set DOTNET_ROOT to the full `.tools/dotnet` directory and prepend it to PATH. The SDK itself is ignored, not shipped. CI runs build/test/publish on Windows; its workflow is added but has not been executed remotely.

UI smoke requires an unlocked interactive Windows desktop. It creates a unique temporary data root/random test-only admin, visits all tabs, saves a disabled camera, logs out/in and waits for shutdown. No real camera or production data is touched. Screenshots go to ignored artifacts/screenshots; temporary test data paths are printed for inspection and later cleanup.

## Upgrade/recovery

Stop the app and back up the entire data root before upgrades. Test migrations/restores on a copy. Preserve the original DPAPI Windows account/profile or plan to reenter secrets. Copying only a live SQLite database can lose WAL changes. There is no downgrade tool, automatic updater, installer, signing or unattended recovery service.

The legacy console project has been removed. Run the WPF application using `dotnet run --project src/SurveillanceAI.Desktop` or the published `artifacts/publish/SurveillanceAI.Desktop.exe`.

## Diagnostics

Dashboard: connection/FPS/latency/drop/reconnect/queue/memory. System: audit trail. Data-root logs: structured JSON. Check camera source/network/driver/codec access using Test saved connection. Validate bundle hash/tensors in System for model failures. Use SMTP connection test and delivery outcomes for email failures, without copying secrets into tickets.

A successful folder publish does not validate hardware capacity, RTSP interoperability, unattended monitoring, commercial licensing or installer certification.
