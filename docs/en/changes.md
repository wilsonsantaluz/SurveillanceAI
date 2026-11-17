[Português](../changes.md) | English

# Changed-file inventory

All changes are local and uncommitted on feature/desktop-foundation. Original weights are preserved. The two initially modified tracked net9.0 obj files are excluded below because they predate this task.

## Modified tracked files

- `Models/DetectionResult.cs`
- `Program.cs`
- `Services/YoloOnnxDetector.cs`
- `SurveillanceAI.csproj`
- `SurveillanceAI.sln`
- `readme.md`

## Added files

- `.github/workflows/windows.yml`
- `.gitignore`
- `Directory.Build.props`
- `docs/architecture.md`
- `docs/audit.md`
- `docs/changes.md`
- `docs/database.md`
- `docs/deployment.md`
- `docs/monitoring.md`
- `docs/security.md`
- `docs/validation.md`
- `global.json`
- `onnxmodel/manifest.json`
- `scripts/Smoke-Desktop.ps1`
- `src/SurveillanceAI.Application/Contracts.cs`
- `src/SurveillanceAI.Application/SurveillanceAI.Application.csproj`
- `src/SurveillanceAI.Desktop/App.xaml`
- `src/SurveillanceAI.Desktop/App.xaml.cs`
- `src/SurveillanceAI.Desktop/FileLogProvider.cs`
- `src/SurveillanceAI.Desktop/MainViewModel.cs`
- `src/SurveillanceAI.Desktop/MainWindow.xaml`
- `src/SurveillanceAI.Desktop/MainWindow.xaml.cs`
- `src/SurveillanceAI.Desktop/SurveillanceAI.Desktop.csproj`
- `src/SurveillanceAI.Desktop/Theme.xaml`
- `src/SurveillanceAI.Domain/Entities.cs`
- `src/SurveillanceAI.Domain/SurveillanceAI.Domain.csproj`
- `src/SurveillanceAI.Infrastructure/AppDbContext.cs`
- `src/SurveillanceAI.Infrastructure/BackgroundWork.cs`
- `src/SurveillanceAI.Infrastructure/Migrations/20261117143508_InitialDesktop.Designer.cs`
- `src/SurveillanceAI.Infrastructure/Migrations/20261117143508_InitialDesktop.cs`
- `src/SurveillanceAI.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `src/SurveillanceAI.Infrastructure/MonitoringStore.cs`
- `src/SurveillanceAI.Infrastructure/ProductStore.cs`
- `src/SurveillanceAI.Infrastructure/Security.cs`
- `src/SurveillanceAI.Infrastructure/SurveillanceAI.Infrastructure.csproj`
- `src/SurveillanceAI.Monitoring/CameraManager.cs`
- `src/SurveillanceAI.Monitoring/CameraWorker.cs`
- `src/SurveillanceAI.Monitoring/ModelBundle.cs`
- `src/SurveillanceAI.Monitoring/OnnxDetector.cs`
- `src/SurveillanceAI.Monitoring/SurveillanceAI.Monitoring.csproj`
- `tests/Fixtures/README.md`
- `tests/Fixtures/bus.jpg`
- `tests/SurveillanceAI.IntegrationTests/AdditionalWorkflowTests.cs`
- `tests/SurveillanceAI.IntegrationTests/MonitoringTests.cs`
- `tests/SurveillanceAI.IntegrationTests/ProductTests.cs`
- `tests/SurveillanceAI.IntegrationTests/SmtpTests.cs`
- `tests/SurveillanceAI.IntegrationTests/SurveillanceAI.IntegrationTests.csproj`
- `tests/SurveillanceAI.UnitTests/RulesTests.cs`
- `tests/SurveillanceAI.UnitTests/SurveillanceAI.UnitTests.csproj`

## Local validation artifacts (ignored)

- `.tools/dotnet`: locally installed SDK; `.tools/dotnet-ef`: migration tooling.
- `artifacts/publish`: self-contained Windows desktop output.
- `artifacts/screenshots`: inspected UI screenshots.
- `artifacts/sample.avi`: public-image test playback fixture, not production footage.

No SDK, build output, database, credential, private footage or generated publish bundle is staged or committed.

## Internationalization additions

Shared bilingual RESX/Text resources, live WPF translation bindings, language preference storage, default Portuguese docs and English copies, resource/culture/persistence tests, and language-independent desktop/localization UI smoke scripts. Persisted identifiers/model classes remain invariant. No commit or push.
