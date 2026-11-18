# Florence-2 e alertas por templates

Português (Brasil) | [English](en/florence-alerts.md)

## Instalação local do modelo

Use o aplicativo publicado em `artifacts/publish/SurveillanceAI.Desktop.exe`, mantendo a pasta completa. Não há API paga, Python, serviço auxiliar ou treinamento. O modelo não é baixado ao abrir o aplicativo.

Para instalar explicitamente o conjunto CPU INT8 (aproximadamente 275 MB de pesos, mais os arquivos de configuração):

```powershell
./scripts/Install-FlorenceModel.ps1
```

O destino padrão é `%LOCALAPPDATA%/SurveillanceAI/models/Florence-2-base-ft`. Para outra pasta, use `-Destination C:/Modelos/Florence-2-base-ft`. O script requer rede durante a instalação, usa `curl.exe`, verifica SHA-256 dos quatro arquivos ONNX e grava `florence-manifest.json`. Depois disso, a inferência funciona offline. Não copie somente um arquivo ONNX: mantenha manifesto, quatro pesos, vocabulário e configurações juntos.

Checkpoint declarado: **microsoft/Florence-2-base-ft**. Exportação: [onnx-community/Florence-2-base-ft](https://huggingface.co/onnx-community/Florence-2-base-ft), revisão `e88a44eaf3791a35eae0c5a47b3dbcd36e67eb6f`, arquivos `*_quantized.onnx`. A [ficha da exportação](https://huggingface.co/onnx-community/Florence-2-base-ft/blob/e88a44eaf3791a35eae0c5a47b3dbcd36e67eb6f/README.md) identifica o checkpoint fine-tuned. Não usamos o download padrão de Florence2-Sharp, que aponta para `base`. Base e base-ft não são tratados como equivalentes. A procedência/hash foi verificada; não foi executada comparação numérica com o checkpoint PyTorch original.

## Configuração do usuário

1. Entre como administrador e abra **Alertas**. A interface simples é a configuração padrão; as regras anteriores continuam em **Regras avançadas**.
2. O Florence é obrigatório e carrega automaticamente na inicialização. Se faltar o modelo, um aviso permanece visível; em **Configurações avançadas**, selecione a pasta instalada e clique **Carregar Florence em CPU** para recuperar. Ative o template **Armas de fogo e simulacros** somente após avaliar suas limitações.
3. Selecione todas as câmeras ou marque câmeras específicas. Em **Objetos selecionados**, escolha os objetos reais do manifesto YOLO. A lista não cria classes novas.
4. Escolha alerta visual, som, evidência e e-mail, depois **Salvar opções de monitoramento**. Eventos são sempre registrados; a primeira evidência de possível arma é obrigatória. E-mail também exige SMTP e destinatários habilitados na tela Notificações.

O Florence permanece obrigatório; o YOLO pode ser habilitado/desabilitado. A instalação local dos pesos é necessária, sem download automático ao abrir. A busca considera a pasta configurada, `SURVEILLANCEAI_FLORENCE_MODEL`, o cache padrão e modelos junto da publicação. Reaplicar configuração reinicia os trabalhadores, como já ocorria para câmeras/modelos. Navegação, idioma e pausa visual não reiniciam inferência. Avisos visuais/sonoros são apresentados na sessão desktop autenticada; não substituem um host persistente.

## O que está disponível

| Template | Situação |
| --- | --- |
| Armas de fogo e simulacros | Conectado ao Florence local, experimental e inicialmente desligado. Sem validação positiva com armas nesta entrega. |
| Objetos selecionados | YOLO existente; lista obtida do manifesto real. Usa confirmação temporal e intervalo internos. |
| Facas e objetos cortantes | Indisponível: o manifesto possui `knife`, mas não houve teste positivo com imagens de facas. |
| Pessoas correndo | Indisponível: associação por IoU existente não comprova corrida ou velocidade. |
| Gritos e pedidos de socorro | Indisponível: não há captura de áudio nem classificador acústico validado. Florence não é modelo de áudio. |
| Movimento fora do horário | Indisponível: não há detector de movimento e agenda operacional implementados. |

## Primeira evidência e episódios

O monitoramento Florence executa detecção de objetos (`OD`) e salva imediatamente uma hipótese de arma encontrada. Se OD não identificar arma, executa novamente a busca por frase (`CAPTION_TO_PHRASE_GROUNDING`) com `handgun . pistol . revolver . firearm . rifle . person holding a handgun`. Esse comportamento foi restaurado a pedido do usuário para avaliar simulacros. Grounding pode retornar caixas mesmo sem arma real; os eventos continuam sendo hipóteses que exigem revisão humana. Nenhuma pessoa, postura ou intenção é exigida.

São salvos câmera, instante UTC de captura, detector, revisão do checkpoint, caixa e primeira imagem. Não há porcentagem de confiança Florence: o campo é nulo e a interface mostra **Não informada**. A imagem de teste não gera incidente por conta própria.

Uma restrição SQLite permite um episódio de possível arma aberto por câmera/template. Repetições não sobrescrevem a primeira imagem nem criam novos envios. Isso também vale após reiniciar o aplicativo. Em Eventos:

- **Reconhecer alerta:** registra que o operador viu o aviso; não confirma arma e mantém a deduplicação.
- **Confirmar visualmente:** marca uma conclusão humana, separada da hipótese do modelo.
- **Encerrar episódio:** fecha o episódio e aguarda uma captura posterior sem hipótese de arma nas duas análises (OD e grounding) antes de permitir outro alerta nessa câmera. A supressão persiste após reiniciar e excluir eventos; quadros antigos não rearmam o alerta.
- **Marcar falso positivo:** registra a revisão e encerra o episódio.

Nenhum estado representa crime confirmado. Regras antigas e suas referências permanecem preservadas; regras geradas por templates não devem ser editadas manualmente.

## Imagem local e diagnóstico

Em **Teste por imagem**, selecione um arquivo de até 16 MB, Florence e, opcionalmente, YOLO. Para localização por frase, marque a opção e informe a descrição em inglês. A tela mostra caixas, descrição, modelo e duração de cada inferência; é possível cancelar. O modelo Florence é compartilhado e reutilizado. O teste usa a configuração local da pasta, não envia imagens externamente e não insere eventos fictícios.

O orçamento padrão é 128 tokens, configurável de 32 a 256. Há uma fila de um quadro por câmera, descarte dos antigos, redimensionamento limitado a 1280 pixels para o ramo Florence e exclusão mútua no modelo compartilhado. O ramo YOLO não aguarda Florence. O Florence usa uma quantidade configurável de threads ONNX de CPU, com padrão de quatro; isso não garante ganho proporcional e pode aumentar a contenção com YOLO. A contagem de descartes Florence inclui quadros omitidos pela amostragem configurada.

Esta publicação usa CPU. O adaptador aceita CUDA/DirectML somente se o runtime nativo correspondente estiver disponível; essa ativação exige distribuição compatível e configuração técnica, não um botão fictício. GPU não foi validada. Não se presume que a MX450 com 2 GB comporte pesos e ativações. Não há promessa de tempo real ou número de câmeras.

## Validação executada

Foram executadas inferências reais CPU de OD e grounding com a imagem pública de ônibus. Houve caixas e latências medidas. O YOLO existente continuou detectando pessoas/ônibus. O teste visual carregou base-ft e executou ambos na mesma tela. Não havia imagens de armas/facas; acurácia para esses objetos, simulacros, falsos positivos e condições reais de lojas **não foi comprovada**.

```powershell
$env:SURVEILLANCEAI_FLORENCE_MODEL = 'C:/Modelos/Florence-2-base-ft'
dotnet test SurveillanceAI.sln -c Release
./scripts/Smoke-SmartMonitoring.ps1 -ModelDirectory $env:SURVEILLANCEAI_FLORENCE_MODEL
```

Sem essa variável, somente o teste que necessita dos pesos Florence é explicitamente ignorado; CI não baixa pesos. Testes com resultados simulados comprovam persistência/deduplicação, não reconhecimento visual de armas. Consulte [validação](validation.md).

## Biblioteca e operação comercial

Foi inspecionada [Florence2-Sharp](https://github.com/curiosity-ai/florence2-sharp), commit `392b9f12bed69c6c76d30bf7c5f8d8fbc5610fff`. A cópia MIT em `third_party/Florence2` contém correções delimitadas de descarte de sessões/resultados, limite de geração por instância, leitura do tokenizer local e ordem RGB. Veja seu `PROVENANCE.md`. Nenhuma alteração foi feita em `OnnxDetector.cs`.

O SQLite aplica migração compatível para metadados de detector/template, confiança anulável e ações do operador. Faça backup da pasta inteira com o aplicativo fechado antes de atualizar. Os pesos instalados não são empacotados no Git ou baixados automaticamente para o cliente. Revise licenciamento, privacidade, imagens representativas, armazenamento, recuperação e desempenho antes de um piloto. O aplicativo ainda para ao ser fechado.

A janela abre maximizada e usa ícone próprio. Campos, listas, calendários e barras de rolagem seguem o tema escuro; detalhes de eventos vazios ficam recolhidos.

## Revisão e exclusão

Reconhecer, confirmar, marcar falso positivo ou encerrar altera somente o registro do evento. Nenhuma dessas ações treina o modelo, muda pesos ou alimenta os próximos prompts. Falso positivo também encerra o episódio e aguarda ausência nas duas análises antes do próximo alerta. A ausência em uma análise é uma condição operacional de rearme, não prova de segurança da cena.

Operadores e administradores podem **Excluir evento**, com confirmação. O evento, a evidência e as notificações registradas são removidos. E-mails já enviados não podem ser recolhidos. Permanecem a auditoria da exclusão e o estado de rearme por câmera, sem imagem ou conteúdo do evento. A interface recarrega o detalhe selecionado após cada ação.

## Evidência ampliada e caixa da detecção

Em **Eventos**, selecione um registro e clique **Ampliar evidência**. A janela abre maximizada, com a caixa amarela nas coordenadas gravadas pelo detector. Use **Região detectada** para focalizar a caixa, **100%** para tamanho real, os botões de zoom ou Ctrl + roda do mouse. **Mostrar caixa da detecção** alterna entre imagem marcada e original; **Salvar imagem…** exporta PNG na resolução salva, respeitando essa opção. O zoom da tela não reduz o arquivo exportado. A caixa representa a hipótese do modelo.

Novos eventos Florence preservam a resolução capturada pela fonte, em JPEG qualidade 95. Só a imagem de entrada da inferência é reduzida para até 1280 pixels no maior lado; as caixas retornadas são convertidas para os pixels da evidência original. A fila continua limitada a um quadro por câmera, agora na resolução capturada, com maior uso de memória conforme a resolução. A imagem original não recebe marcações permanentes. Eventos antigos podem ser ampliados e anotados usando suas coordenadas, mas os detalhes perdidos na redução anterior não podem ser recuperados.

## Estado por câmera

O cartão de cada câmera mostra o estado do Florence, o total de análises concluídas, a duração da última e seu horário UTC. São métricas do trabalhador da câmera, separadas das inferências manuais por imagem. Câmeras fora da seleção são identificadas explicitamente. Falhas de carregamento são apresentadas e tentadas novamente a cada cinco segundos, sem interromper o YOLO. Um modelo carregado globalmente não comprova, sozinho, que uma câmera está sendo analisada.

## Ajuste de threads de CPU

Em **Alertas → Configurações avançadas → Threads de CPU do Florence (1–64)**, escolha o valor e clique **Salvar opções de monitoramento**. O modelo é recriado com a quantidade escolhida e a preferência persiste após reiniciar. O diagnóstico mostra **Threads de CPU ativas**. **Carregar Florence em CPU** e o teste por imagem também usam o valor do formulário; salvar é necessário para memorizar. Perfis antigos usam quatro por padrão.

Para comparar, use a mesma cena e teste 4, 6, 8 ou outros valores. O limite de 64 permite experimentação; valores acima da capacidade física não garantem ganho e podem piorar latência ou desempenho do YOLO. O parâmetro ajusta threads internas dos operadores ONNX; o modelo compartilhado continua processando uma análise por vez.

## Arquivos de vídeo: quadro a quadro

**Analisar todos os quadros de arquivos de vídeo** fica habilitado por padrão em Alertas → Configurações avançadas. Para fontes do tipo Arquivo de vídeo, o leitor espera concluir Florence antes de avançar, sem intervalo de amostragem nem descarte por fila. O YOLO recebe os mesmos quadros com fila limitada que aguarda espaço. Ao chegar ao fim, a análise termina; não reinicia o vídeo automaticamente. O cartão mostra o quadro processado e a posição aproximada em segundos (calculada pelo FPS declarado do arquivo). Eventos incluem o índice do quadro nas notas. O horário UTC continua sendo o momento do processamento, não a data original da filmagem.

O vídeo avança mais devagar que a gravação: com 20 FPS e 4 segundos por quadro, um minuto pode levar cerca de 80 minutos para uma tarefa, e OD/grounding/confirmação podem aumentar esse tempo. Não há retomada persistente: reiniciar o aplicativo ou reaplicar configurações começa novamente do início. Quadros decodificados são processados em ordem; erros de leitura do arquivo e falhas de armazenamento continuam exigindo verificação. Falhas de inferência no modo sequencial mantêm o mesmo quadro para nova tentativa.

Webcam, RTSP e HTTP continuam no modo ao vivo com amostragem e fila do quadro mais recente. Cobertura integral dessas fontes requer gravação e análise posterior; ainda não foi implementada. Desmarcar o modo sequencial também restaura o comportamento anterior dos arquivos.

## Confirmação de hipóteses encontradas por frase

**Exigir confirmação por detecção de objetos no recorte indicado** fica habilitado por padrão. OD no quadro completo continua gerando a primeira evidência imediatamente. Quando apenas grounding encontra arma, executa-se OD no recorte marcado. Sem classe de arma no recorte, não se cria evento nem se mantém a caixa de arma na prévia. O arquivo original continua preservado quando há evento.

É uma segunda análise com o mesmo modelo, não uma validação independente nem garantia contra falsos positivos. Pode perder armas e simulacros que o OD não reconhece. Desmarcar a opção restaura hipóteses diretas de grounding. A confirmação não usa pessoas, postura ou intenção. Os testes com recortes simulados verificam o fluxo; não comprovam acurácia na imagem do monitor enviada pelo usuário.
