Português (Brasil) | [English](en/audit.md)

# Auditoria e migração incremental

Foram inspecionados fontes, solução, exportação, README, gerador de imagens, inventário e ONNX antes de migrar. Origem wilsonsantaluz/SurveillanceAI; branch feature/desktop-foundation; sem commit/push autorizado.

- Projeto era net9.0, apesar de o README dizer .NET 8. SDK 10.0.404 foi instalado em .tools/dotnet ignorado.
- Program concentrava captura/inferência/alertas, fixava webcam 0, repetia falhas sem espera e não liberava detector/quadro.
- Detector fixava 640×640, confiança 0,65, IoU 0,45, COCO/saída por atributos e aplicava Sigmoid sem verificar semântica.
- Entrada real float32 images[batch,3,height,width]; saída output0[batch,84,anchors]. Concat junta caixas a Sigmoid: já são probabilidades. Metadados: Ultralytics 8.4.37, COCO, dinâmico, opset 12, sem NMS integrado, AGPL-3.0.
- Cópias ONNX: SHA-256 56caff0a3c1c2f20a7b7839cf7ba697d1198b051e037554ef00842a1b2714625. Todos os pesos ONNX/PT foram preservados.
- ProximityAnalyzer usava listas/limites arbitrários e EventAnalyzer chamava duas pessoas de suspeitas. Não integram alertas desktop. O console não os chama; distância é diagnóstico opcional.
- CameraService era RTSP síncrono e Logger só console. Não havia UI, autenticação, eventos, SMTP, manifesto ou testes.
- Há obj versionado; dois net9.0 já alterados foram preservados. Console agora usa obj/legacy; limpeza histórica deve ser revisada separadamente.

O caminho incremental preservou console/pesos, adicionou módulos, validou autenticação/migrações, reutilizou RGB/letterbox/NMS com semântica comprovada, conectou WPF a serviços reais, testou vídeo/regras/revisão/SMTP/retenção e publicou/abriu o executável.

A inspeção somente leitura do treinamento cobriu README, deployment/manifest.py e export.py em main. Era classificação ResNet18/ImageNet/logits [batch,classes], não caixas de objetos. [Fonte](https://github.com/wilsonsantaluz/Surveillance-AI-Training/blob/main/src/surveillance_ml/deployment/manifest.py). Nenhum arquivo daquele repositório foi alterado.

A pedido do usuário, interface/documentação passam a português por padrão com suporte a inglês, preservando códigos e classes persistidos.
