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
