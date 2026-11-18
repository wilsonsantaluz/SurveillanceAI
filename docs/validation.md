Português (Brasil) | [English](en/validation.md)

# Validação executada

As verificações foram locais em Windows x64/SDK 10.0.404, não execução remota do GitHub Actions. A base anterior passou em Release sem avisos/erros, 27 testes, publicação, configuração inicial, entrada/saída, abas, câmera, SQLite e encerramento. Vídeo mostrou caixas ONNX reais, métricas, pausa e tela cheia. SMTP enviou somente ao servidor local; auditoria de dependências não indicou vulnerabilidades nas fontes consultadas.

## Resultado após a internacionalização

- **35 testes aprovados**: 15 unitários e 20 de integração, sem falhas ou testes ignorados.
- Publicação Windows x64 concluída; o assembly satélite inglês foi incluído.
- Teste visual de vídeo em português aprovado: câmera, inferência, pausa e tela cheia.
- Smoke-Localization.ps1 aprovado: português padrão, validação traduzida, troca pt/en, formulário/sessão preservados e preferência restaurada após reiniciar.
- Capturas login-pt-BR.png e dashboard-en-US.png inspecionadas.

## Cobertura

- Hash/senha, configuração inicial, bloqueio, ativação, perfis e proteção contra autodesativação.
- Autorização em serviços, migrações temporárias, câmera/destinatários e segredos protegidos.
- Eventos, paginação/filtros, anotações/revisão, contadores, fila transacional e unicidade.
- Falhas/repetições SMTP persistidas, erros sem segredos, transporte simulado e MailKit em loopback.
- Caminhos protegidos, imagens e retenção; ONNX real, probabilidades, tensores/manifestos/hashes inválidos.
- MJPEG, confirmação, isolamento/expiração/IoU/intervalo, reconexão, descarte/liberação e cancelamento.
- Internacionalização: paridade português/inglês, bindings, formatos numéricos, códigos preservados e preferência persistida/recuperada de arquivo inválido.

Compile com dotnet build, execute dotnet test e publique o projeto Desktop. scripts/Smoke-Desktop.ps1 usa instalação temporária e câmera desabilitada; -VideoPath aceita vídeo local. O vídeo local foi gerado da imagem pública, não de filmagem privada. Capturas inspecionadas ficam em artifacts/screenshots. Isso comprova funcionamento, não desempenho operacional ou acurácia.

Webcam/RTSP reais, rede, drivers bloqueados, GPU, carga prolongada, disco cheio, energia/recuperação e assinatura/instalador ainda exigem campo. Não se comprova crime/comportamento/armas ou disponibilidade contínua. Host persistente, reconciliação por câmera, auditoria longa e órfãos são futuros. Pesos e alterações preexistentes foram preservados; nenhum commit/push. Veja [arquivos](changes.md).

## Evolução Florence-2/templates

Foram executados OD e grounding reais em CPU com base-ft quantizado e a imagem pública de ônibus. O teste visual carregou o modelo, salvou templates e mostrou YOLO/Florence com caixas, duração e confiança Florence não informada. Testes verificam migração com dados legados, evidência imediata, deduplicação, ações humanas, câmera YOLO independente de Florence lento, cancelamento e arquivos inválidos. Não havia imagens de armas/facas: resultados simulados nesses testes verificam semântica dos eventos, não capacidade visual. Veja [registro da entrega](florence-alerts.md).

Validação final: 50 testes aprovados (15 unitários e 35 de integração), sem ignorados, com o conjunto base-ft local configurado. Smoke desktop com vídeo, troca pt/en, abertura maximizada, inicialização automática do Florence, OD/grounding reais e encerramento durante inferência aprovados. Calendário, filtros e navegação das telas foram inspecionados visualmente.

## Correção de revisão e exclusão de eventos

50 testes aprovados após a correção: grounding não cria alertas automáticos; fechamento/falso positivo aguardam ausência posterior, inclusive após reiniciar e excluir todos os eventos. Exclusão remove evidência e notificações e rejeita perfil somente leitura. Teste UI com instalação temporária e câmera desabilitada confirmou detalhe atualizado após encerrar, cancelamento da exclusão preservando o evento e exclusão confirmada. O evento de teste foi inserido somente no banco temporário. Publicação desta correção em `artifacts/publish-event-fix`, pois a publicação anterior estava em execução.

Restauração solicitada pelo usuário: fallback automático de grounding novamente habilitado quando OD não encontra arma. A ausência somente em OD não rearma um episódio; é necessário também não haver hipótese no grounding. 50 testes aprovados, incluindo persistência de evento de grounding e bloqueio de rearme quando apenas OD está negativo. Publicação principal atualizada em `artifacts/publish`.

Evidência ampliada: 51 testes aprovados (15 unitários e 36 de integração). Fixture 2560×1440 persistida sem redução, entrada de inferência 1280×720 e caixa convertida corretamente. Smoke UI com imagem pública e caixa de fixture verificou janela ampliada, foco na região, zoom, tamanho real, alternância de caixa e exportação PNG nas dimensões originais. Não é validação de reconhecimento de armas.

Diagnóstico Florence por câmera: 51 testes aprovados. Teste com modelo real passou também pelo Offer/fila/worker da câmera e verificou contador e horário de conclusão; teste de evidência simula falha inicial do modelo e comprova recuperação automática antes de salvar o evento. Smoke desktop com `-VideoPath artifacts/sample.avi -RequireFlorence` exige ao menos uma análise Florence concluída no cartão da câmera.

Threads configuráveis: 51 testes aprovados. Testes verificam persistência de 8 threads, padrão 4 em perfis legados, rejeição de 0/65 e recriação real do modelo entre 6 e 8 threads. Smoke UI selecionou 6, executou OD/grounding, salvou, reiniciou e confirmou campo restaurado e diagnóstico com 6 threads ativas. Esses checks comprovam aplicação da configuração, não ganho de desempenho.

Quadro a quadro/confirmação: 55 testes aprovados (15 unitários e 40 de integração). Fonte finita de três quadros processada em ordem, sem descarte, com finalização no EOF sem loop. Teorias verificam recorte OD negativo bloqueando evento, positivo permitindo e opção desativada restaurando grounding direto. Mock de monitor não valida a imagem real enviada. Smoke desktop/vídeo/Florence e smoke de configurações/modelos reais passaram. Modo sequencial aplica-se a arquivos; fontes ao vivo permanecem amostradas.
