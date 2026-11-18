# Florence2-Sharp — origem e alterações locais

- Fonte: https://github.com/curiosity-ai/florence2-sharp
- Commit inspecionado: `392b9f12bed69c6c76d30bf7c5f8d8fbc5610fff`.
- Licença: MIT, preservada em `LICENSE`. Esta cópia não é uma biblioteca independente nova nem um projeto de treinamento.
- Compilação restrita a .NET 10, ONNX Runtime 1.24.4 e SkiaSharp 4.153.1. A dependência ML.NET não era usada por esses fontes e não foi adicionada. O downloader foi excluído: o aplicativo usa apenas arquivos locais verificados.

A API pública inspecionada não implementava `IDisposable` e criava sessões TopK por chamada sem liberá-las. Por isso usamos uma cópia pequena e rastreável em vez de reflexão sobre campos privados de um pacote. Mudanças:

1. Implementar descarte das quatro sessões, inclusive em falha parcial de construção. `SessionOptions` pertence ao adaptador chamador e é descartado por ele.
2. Liberar sessão e resultados auxiliares TopK por inferência.
3. Ler vocabulário/configuração do tokenizer do pacote base-ft local, sem depender do tokenizer base embutido.
4. Corrigir ordem dos canais para o bitmap explicitamente decodificado como `Rgba8888` (R em índice 0, B em índice 2).
5. Limitar geração por instância (`MaxLength`), sem alterar configuração global de outros consumidores.
6. Verificar cancelamento antes do processamento e preservar a interrupção nativa via `RunOptions.Terminate` existente.

Os avisos de variável não usada/código inacessível preexistentes foram suprimidos somente neste projeto de terceiros; as anotações anuláveis são preservadas sem ativar uma refatoração geral. Não alteramos YOLO nem alegamos correção/validação geral da biblioteca. O algoritmo de geração original foi preservado; não foi feita comparação de paridade com Transformers/PyTorch.

Os modelos ONNX não estão aqui nem no Git. O instalador usa a exportação `onnx-community/Florence-2-base-ft` em revisão fixa, com hashes de pesos fixos. A integração foi validada em CPU com imagem pública, não com armas ou facas. GPU e operação contínua não foram validadas.
