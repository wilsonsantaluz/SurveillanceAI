Português (Brasil) | [English](en/monitoring.md)

# Monitoramento e contrato de modelos

Cada câmera habilitada tem captura, canal de um quadro e inferência. Mats substituídos são liberados. Captura, inferência, banco e JPEG não usam o dispatcher. Prévias são consultadas duas vezes por segundo e painel/eventos a cada dez, sem substituir um evento selecionado durante anotações.

Webcam usa índice não negativo e DirectShow. RTSP usa FFmpeg com limites de 5 s para abrir e 3 s para ler. Vídeos seguem FPS da fonte ou 25 FPS como alternativa. Fim de arquivo é desconexão e a reprodução repete após espera. Espera dobra de 1 até 30 s; um quadro recebido reinicia falhas. Desconexão limpa confirmação. Cancelamento não interrompe todo driver nativo: hardware real precisa de testes.

Métricas medidas: FPS, latência, fila 0–1, descartes, reconexões e memória. FPS configurado é máximo; processamento soma tempo à espera. Não há promessa de desempenho/quantidade de câmeras. Diagnóstico nativo é silenciado para proteger URLs; registros usam GUID/tipo de erro.

## Pacote de detecção v1

onnxmodel/manifest.json é o exemplo funcional. Pacote contém manifesto e ONNX indicado. Campos: schemaVersion=1, modelFile (nome simples), sha256 real, version, classMapVersion, labels únicas, inputName/outputName, width/height, layout, scores, preprocessing, confidence e iou. Calcule hash com `Get-FileHash model.onnx -Algorithm SHA256`; não copie hash de outro modelo. Não há download automático.

Pré-processamento: BGR para RGB, letterbox central com preenchimento 114, float32 NCHW dividido por 255. Dimensões múltiplas de 32, entre 32 e 2048. Uma entrada/saída; lote 1 ou dinâmico, três canais. Saída: cx/cy/w/h decodificados em pixels da entrada e C pontuações, `[1,4+C,N]` (featuresFirst) ou `[1,N,4+C]` (boxesFirst). Classes correspondem aos atributos. Coordenadas normalizadas, objectness separado, NMS integrado, segmentação e classificação são rejeitados.

scores declara probabilities (0–1, uso direto) ou logits independentes (Sigmoid uma vez). Não há softmax. Dimensões/valores finitos são verificados; caixas voltam ao quadro original e recebem NMS por classe. O grafo existente já usa Sigmoid; a aplicação dupla foi corrigida após inspeção e é testada.

A fase 3 inspecionada do treinamento era ResNet18, ImageNet e logits `[batch,classes]`, incompatível intencionalmente. Detector futuro exige contrato/validação próprios; não se pressupõe armas.

## Confirmação temporal

Regras usam classe/confiança e câmera opcional. IoU permite um acerto por trilha por quadro/instante. Acertos expiram na janela; múltiplos confirmam uma possível detecção, respeitando intervalo por trilha. Trilhas expiram/reiniciam ao desconectar. Isso não identifica pessoas ou comportamento; oclusão/movimento podem dividir trilhas e duplicar eventos.

Proximidade experimental não gera alertas. Linhas são diagnóstico; pessoas próximas/múltiplas não implicam agressão/roubo. Classes como person e bus permanecem canônicas nos dois idiomas para preservar regras, metadados e compatibilidade.
