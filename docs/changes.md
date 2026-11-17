Português (Brasil) | [English](en/changes.md)

# Arquivos alterados

Alterações locais sem commit em feature/desktop-foundation. Pesos preservados. Os dois obj/net9.0 já modificados não foram alterados por este trabalho.

## Base

- `Models/DetectionResult.cs`
- `Program.cs`
- `Services/YoloOnnxDetector.cs`
- `SurveillanceAI.csproj`
- `SurveillanceAI.sln`
- `readme.md`
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

## Internacionalização

- Application/Localization: Texts.cs, AssemblyInfo.cs e recursos Strings.resx/Strings.en.resx.
- Desktop/Localization/TrExtension.cs, bindings e seleção de idioma em MainWindow/Theme/MainViewModel/App.
- Infrastructure/LanguagePreferences.cs e validações traduzíveis nos serviços.
- LocalizationTests.cs e LanguagePreferencesTests.cs.
- readme.en.md, docs/en, guias portugueses e internationalization.md.
- Teste visual com identificadores independentes do idioma.

SDK/ferramentas, publicação, capturas e vídeo de teste ficam em .tools/artifacts ignorados. Nenhum banco, segredo ou filmagem privada foi preparado para commit ou enviado.
