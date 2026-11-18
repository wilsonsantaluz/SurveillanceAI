# Arquivos da entrega Florence e revisão da interface

Branch: `feature/florence-alert-templates`. Alterações locais, sem commit/push.

Florence obrigatório com inicialização automática, templates simples, episódios persistentes, teste por imagem e revisão visual da aplicação. O motor YOLO `OnnxDetector.cs` foi preservado. Pesos, bancos, SDK e capturas estão ignorados pelo Git.

Validação: 50 testes aprovados com modelo real local; smokes desktop/vídeo, pt/en, Florence/imagem e layout aprovados. Não houve validação positiva de armas ou facas.

## Arquivos

- `SurveillanceAI.sln`
- `docs/database.md`
- `docs/en/database.md`
- `docs/en/florence-alerts.md`
- `docs/en/monitoring.md`
- `docs/en/validation.md`
- `docs/florence-alerts.md`
- `docs/florence-changes.md`
- `docs/monitoring.md`
- `docs/validation.md`
- `readme.en.md`
- `readme.md`
- `scripts/Generate-AppIcon.ps1`
- `scripts/Install-FlorenceModel.ps1`
- `scripts/Smoke-Layout.ps1`
- `scripts/Smoke-SmartMonitoring.ps1`
- `src/SurveillanceAI.Application/Contracts.cs`
- `src/SurveillanceAI.Application/Localization/Strings.en.resx`
- `src/SurveillanceAI.Application/Localization/Strings.resx`
- `src/SurveillanceAI.Application/SmartMonitoring.cs`
- `src/SurveillanceAI.Desktop/App.xaml.cs`
- `src/SurveillanceAI.Desktop/Assets/App.ico`
- `src/SurveillanceAI.Desktop/Assets/App.svg`
- `src/SurveillanceAI.Desktop/Localization/TrExtension.cs`
- `src/SurveillanceAI.Desktop/MainViewModel.Smart.cs`
- `src/SurveillanceAI.Desktop/MainViewModel.cs`
- `src/SurveillanceAI.Desktop/MainWindow.xaml`
- `src/SurveillanceAI.Desktop/MainWindow.xaml.cs`
- `src/SurveillanceAI.Desktop/SurveillanceAI.Desktop.csproj`
- `src/SurveillanceAI.Desktop/Theme.xaml`
- `src/SurveillanceAI.Domain/Entities.cs`
- `src/SurveillanceAI.Infrastructure/AppDbContext.cs`
- `src/SurveillanceAI.Infrastructure/BackgroundWork.cs`
- `src/SurveillanceAI.Infrastructure/Migrations/20261117213722_SmartAlertEpisodes.Designer.cs`
- `src/SurveillanceAI.Infrastructure/Migrations/20261117213722_SmartAlertEpisodes.cs`
- `src/SurveillanceAI.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `src/SurveillanceAI.Infrastructure/MonitoringStore.cs`
- `src/SurveillanceAI.Infrastructure/ProductStore.cs`
- `src/SurveillanceAI.Infrastructure/SmartConfigStore.cs`
- `src/SurveillanceAI.Monitoring/CameraManager.cs`
- `src/SurveillanceAI.Monitoring/CameraWorker.cs`
- `src/SurveillanceAI.Monitoring/FlorenceCameraWorker.cs`
- `src/SurveillanceAI.Monitoring/FlorenceDetector.cs`
- `src/SurveillanceAI.Monitoring/FlorenceStartupService.cs`
- `src/SurveillanceAI.Monitoring/SurveillanceAI.Monitoring.csproj`
- `tests/SurveillanceAI.IntegrationTests/SmartMonitoringTests.cs`
- `third_party/Florence2/`: adaptador MIT vendorizado, recursos do tokenizer, licença e procedência documentada em `PROVENANCE.md`.

## Correção de ações do operador

Adicionado `src/SurveillanceAI.Infrastructure/EpisodeRearm.cs`: estado de rearme persistente em Settings, sem migração adicional. Atualizados worker Florence (alertas somente OD), EventWriter/MonitoringStore/Contracts (ausência posterior), ProductStore (exclusão e rearme), MainViewModel e XAML (detalhe atualizado e exclusão com confirmação), traduções e teste de integração de episódios. Guia de uso e validação atualizados em português/inglês.

## Evidência ampliada

Novos `Desktop/EvidenceWindow.cs` e `Desktop/EvidenceRendering.cs`: visualização, zoom, caixa e exportação PNG. Atualizados MainViewModel/XAML/traduções, FlorenceCameraWorker (captura original e coordenadas escaladas), teste de integração de resolução e guias PT/EN.

Threads CPU configuráveis (1–64): atualizado SmartMonitoringSettings/interface, adapter ONNX, inicialização, worker, storage, teste por imagem e UI. Mudança recria sessões sob exclusão mútua; valores antigos padrão 4. Testes de persistência/limites/recriação e smoke de restauração após reiniciar.

Modo sequencial e confirmação: CameraWorker aguarda ProcessFrameAsync antes de ler próximo quadro de arquivo, usa fila com espera e encerra no EOF; CameraManager seleciona o modo por tipo de fonte/configuração. FlorenceCameraWorker compartilha processamento entre fila ao vivo e modo sequencial, tenta novamente o mesmo quadro após falhas, verifica grounding com OD no recorte e inclui índice do quadro nas notas. SmartMonitoringSettings, CameraSnapshot, XAML, métricas/traduções e SequentialMonitoringTests atualizados.
