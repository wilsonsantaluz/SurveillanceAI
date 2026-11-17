Português (Brasil) | [English](en/deployment.md)

# Implantação Windows

Use Windows x64, SDK .NET 10 e NuGet na restauração. A validação local usou SDK 10.0.404. Dependências têm versões fixadas; SQLite nativo foi elevado acima da versão transitiva vulnerável antiga.

Compile com `dotnet build SurveillanceAI.sln -c Release`, teste com `dotnet test SurveillanceAI.sln -c Release` e publique com `dotnet publish src/SurveillanceAI.Desktop -c Release -r win-x64 --self-contained true -o artifacts/publish`.

Abra artifacts/publish/SurveillanceAI.Desktop.exe e mantenha toda a pasta, bibliotecas e models/legacy. Inclui runtime desktop; não é instalador assinado ou arquivo único. Valide Windows/CPU, drivers/codecs, permissões e disco. SDK local exige DOTNET_ROOT/PATH para .tools/dotnet e não é distribuído. Não se afirma execução remota da integração contínua sem publicá-la.

## Idioma e teste visual

Português é padrão. Idioma troca para inglês antes/depois de entrar, sem reiniciar câmeras, e salva language.json. Arquivo ausente/inválido/desconhecido volta ao português. A preferência acompanha o backup da instalação.

Execute `./scripts/Smoke-Desktop.ps1` em desktop interativo desbloqueado. Ele cria dados/senha temporários, navega por IDs estáveis, cadastra câmera desabilitada, sai/entra e aguarda encerramento. Não acessa produção/hardware. Com `-VideoPath C:/caminho/teste.avi`, verifica vídeo, pausa e tela cheia. Capturas ficam em artifacts/screenshots; o caminho temporário é informado para inspeção/limpeza.

## Atualização, recuperação e diagnóstico

Feche e copie toda a pasta antes de atualizar; teste migrações/restauração em cópia. Preserve conta/perfil DPAPI ou reinforme segredos. Copiar só SQLite ativo pode perder WAL. Não há downgrade, atualizador, instalador/assinatura ou recuperação autônoma.

O projeto antigo de console foi removido. Execute exclusivamente o aplicativo WPF com `dotnet run --project src/SurveillanceAI.Desktop` ou o binário publicado `artifacts/publish/SurveillanceAI.Desktop.exe`. O diagnóstico de proximidade continua disponível apenas como opção visual no desktop; não é regra de alerta.

Painel mostra métricas; Sistema mostra auditoria; logs contém JSON técnico. Use conexão salva para fonte/rede/driver/codec, validação de modelo para hash/tensores e SMTP/envios para correio. Não exponha segredos em chamados. Publicação bem-sucedida não valida capacidade, RTSP, autonomia, licença comercial ou instalador.

Teste da troca e persistência de idioma: `./scripts/Smoke-Localization.ps1`.
