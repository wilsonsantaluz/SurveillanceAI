# SurveillanceAI — base do aplicativo Windows

Português (Brasil) | [English](readme.en.md)

Central local em WPF para monitorar câmeras, detectar objetos e revisar eventos. Esta base não prevê crimes, não reconhece agressões e não oferece detector de armas validado. **O monitoramento para quando o aplicativo é fechado.**

## Funcionalidades implementadas

- .NET 10, WPF, MVVM e Generic Host; tema escuro, grade de câmeras, tela cheia e pausa da prévia sem interromper a inferência.
- Criação inicial do administrador, senhas com hash Identity, entrada/saída, alteração de senha, três perfis, ativação e bloqueio por tentativas inválidas.
- SQLite com migrações EF Core, cadastro de câmeras, eventos paginados e filtráveis, revisão, anotações e auditoria. Nenhum incidente fictício é inserido na instalação.
- Captura/inferência em segundo plano, fila de um quadro, descarte/liberação dos antigos, webcam/RTSP/vídeo local, reconexão e cancelamento.
- Inferência ONNX real em CPU com YOLOv8n, manifesto, SHA-256, classes versionadas, validação de tensores e NMS.
- Regras inicialmente desabilitadas, confirmação temporal, associação espacial e intervalo entre alertas. Proximidade é apenas diagnóstico visual opcional.
- Destinatários com filtros, credenciais DPAPI, MailKit, fila persistente, novas tentativas e testes SMTP.
- Imagens opcionais de eventos, retenção, registros JSON configuráveis e métricas medidas.
- Português por padrão, inglês selecionável durante o uso, testes automatizados, integração contínua e publicação Windows independente do runtime instalado.

## Idioma

A interface inicia em **português do Brasil (pt-BR)**, independentemente do Windows. No seletor **Idioma**, disponível antes e depois de entrar, escolha **English** para inglês (en-US). A troca é imediata e não reinicia as câmeras.

A preferência fica em `language.json`, na pasta da instalação, e é restaurada ao abrir. Números e datas seguem o idioma. Identificadores do banco, classes do modelo e valores técnicos permanecem estáveis; sua apresentação é traduzida. Os guias principais estão em português e vinculam versões inglesas. Veja [internacionalização](docs/internationalization.md).

## Arquitetura

```mermaid
flowchart TD
    Desktop[WPF / MVVM / Generic Host] --> Infrastructure[SQLite / autenticação / evidências / SMTP]
    Desktop --> Monitoring[Câmeras / ONNX / quadros limitados]
    Infrastructure --> Application[Contratos / autorização / regras temporais / tradução]
    Monitoring --> Application
    Application --> Domain[Entidades / detecções / perfis]
```

O projeto de console legado foi retirado da solução. O executável principal é `SurveillanceAI.Desktop`; os modelos ONNX necessários foram preservados. Os cinco projetos de produção não têm dependências circulares. Consulte [arquitetura](docs/architecture.md) e [auditoria](docs/audit.md).

## Compilar e iniciar

Use Windows x64 e SDK .NET 10. O global.json aceita a faixa de recursos mais recente do .NET 10; o Visual Studio precisa ser compatível. A linha de comando funciona separadamente.

```powershell
dotnet restore SurveillanceAI.sln
dotnet build SurveillanceAI.sln
dotnet run --project src/SurveillanceAI.Desktop
```

Nesta máquina, `.tools/dotnet/dotnet.exe` pode substituir dotnet. Esse SDK local é ignorado pelo Git e não faz parte do produto.

A primeira abertura cria e migra `%LOCALAPPDATA%/SurveillanceAI/surveillance.db`. Informe usuário e senha de **12 a 256 caracteres** para criar o administrador e entrar. Não há senha padrão de produção. Apenas um processo usa a mesma pasta por vez.

Defina `SURVEILLANCEAI_DATA` antes de abrir para usar uma pasta local privada fora do repositório. Isso muda banco, evidências, registros e preferência de idioma juntos. Feche antes de copiar a instalação. Credenciais protegidas dependem da conta Windows original.

## Operação

1. **Câmeras:** crie uma câmera com nome, descrição, localização, origem e FPS (1–30). Use 0 para a webcam padrão, caminho completo para vídeo ou URI RTSP. A origem é visível durante a digitação e protegida no armazenamento; vazia na edição preserva a anterior. Selecione `HttpStream` para streams HTTP/HTTPS, como os do LensCast. Salve e teste. Habilite/desabilite e salve para iniciar/parar; reaplicar configuração reinicia os trabalhadores configurados.
2. **Visão geral e ao vivo:** acompanhe conexão, vídeo, modelo, FPS, latência, descartes, reconexões e fila. Amplie para tela cheia; Escape retorna. Pausar a prévia mantém a inferência. Nenhuma câmera é cadastrada automaticamente.
3. **Regras de alerta:** use a classe exata do manifesto, confiança, confirmações, janela, IoU, intervalo e severidade. Um ID opcional limita por câmera. Habilite explicitamente. Valores iniciais são exemplos que exigem validação. A classe canônica continua sendo, por exemplo, `person`, mesmo em português.
4. **Eventos:** busque classe/anotações e filtre câmera, datas locais, severidade e revisão. Horários dos eventos são UTC, com formato regional. Páginas têm até 100 registros. Operadores/administradores podem anotar, revisar e marcar falsos positivos; visualizadores não alteram eventos.
5. **Sistema:** valide e ative pacotes locais confiáveis; configure imagens de evidência, retenção e nível dos registros. Evidências ficam em evidence na pasta de dados. Retenção padrão: 30 dias para eventos/imagens e 14 para registros.
6. **Usuários / Minha conta:** gerencie contas ou altere sua senha. Serviços verificam permissões além da visibilidade dos controles.

## Modelos

`onnxmodel/yolov8n.onnx` e manifest.json são publicados em models/legacy. As cópias históricas têm o mesmo SHA-256 e foram preservadas; a publicação desktop não inclui duplicatas ou .pt. Valide um pacote com manifesto/ONNX em Sistema antes de ativar. Não há download automático. Veja o [contrato de modelos](docs/monitoring.md).

Na inspeção do treinamento, a fase 3 exportava um classificador ResNet18, não um detector de objetos; esse manifesto é rejeitado. Um detector futuro exige contrato/validação próprios. Não se afirma detecção de armas nem se altera o repositório de treinamento.

## Notificações por e-mail

Em **Notificações**, salve servidor, porta, TLS, usuário/senha opcionais, remetente e máximo de tentativas. Servidores reais exigem STARTTLS ou TLS ao conectar. O teste local permite SMTP sem criptografia apenas no próprio computador. Cadastre destinatários com filtros de classe/câmera/severidade e habilite o canal explicitamente.

**Testar conexão SMTP salva** conecta/autentica sem enviar. **Enviar e-mail de teste** enfileira uma mensagem mesmo com o canal geral desabilitado. Ambos usam dados salvos. Clique em Atualizar para acompanhar tentativas e erros sem segredos. Notificações normais iniciam desabilitadas e levam metadados, sem anexos de filmagens.

## Testes e publicação

```powershell
dotnet test SurveillanceAI.sln
dotnet publish src/SurveillanceAI.Desktop -c Release -r win-x64 --self-contained true -o artifacts/publish
./scripts/Smoke-Desktop.ps1
```

Testes usam bancos temporários, fontes simuladas, vídeo MJPEG gerado, modelo real em CPU, imagem pública e SMTP local. Não exigem câmera, GPU, conta de e-mail ou rede durante a execução. O teste visual usa dados isolados e câmera desabilitada por padrão; -VideoPath permite vídeo local. A integração contínua compila/testa/publica; o teste visual exige desktop Windows interativo.

Guias: [validação](docs/validation.md), [implantação](docs/deployment.md), [banco](docs/database.md), [segurança](docs/security.md) e [arquivos alterados](docs/changes.md).

## Glossário das tecnologias

Este glossário descreve o papel de cada tecnologia na arquitetura. Tecnologias em avaliação estão identificadas como **planejadas**, não como recursos já disponíveis.

| Tecnologia / conceito | O que é | Papel no SurveillanceAI |
| --- | --- | --- |
| **C# / .NET 10** | Linguagem e plataforma de aplicações. | Implementam a aplicação, o processamento, os serviços e a lógica de monitoramento. |
| **WPF** | Framework de interface desktop para Windows. | Exibe câmeras, configurações, eventos e alertas. |
| **MVVM / CommunityToolkit.Mvvm** | Padrão de separação entre apresentação e lógica de interface, com ferramentas de apoio. | Organiza as telas e o estado da aplicação. |
| **Generic Host** | Infraestrutura .NET para injeção de dependências e serviços hospedados. | Coordena os serviços e o ciclo de vida da aplicação. |
| **YOLOv8n (You Only Look Once)** | Detector neural de objetos da família YOLO. | Identifica classes predefinidas, como pessoas e celulares, retornando classes e caixas delimitadoras; não possui uma classe de arma de fogo validada neste projeto. |
| **ONNX** | Formato interoperável para modelos de aprendizado de máquina. | Permite empacotar e executar o detector sem o framework usado no treinamento. |
| **ONNX Runtime** | Motor de inferência de modelos ONNX. | Executa a detecção localmente; a implementação atual usa CPU. |
| **Inferência** | Execução de um modelo já treinado em novos dados. | Analisa quadros de vídeo sem realizar novo treinamento. |
| **Bounding box** | Retângulo que delimita aproximadamente um objeto na imagem. | Mostra a posição de uma detecção e ajuda a registrar evidências. |
| **Confiança / score** | Pontuação devolvida por um detector para uma hipótese. | Ajuda a filtrar detecções, mas não representa necessariamente uma probabilidade calibrada. |
| **NMS (Non-Maximum Suppression)** | Processo de remoção de caixas redundantes. | Reduz múltiplas detecções sobre o mesmo objeto. |
| **IoU (Intersection over Union)** | Medida de sobreposição entre regiões. | Apoia a associação espacial existente; não equivale a rastrear a mesma pessoa ao longo do tempo. |
| **RTSP** | Protocolo de controle de transmissão de mídia em rede. | Conecta fontes de vídeo compatíveis, como câmeras IP. |
| **HTTP / MJPEG** | Transporte de imagens ou quadros pela rede. | Permite fontes HTTP compatíveis, incluindo streams de dispositivos móveis. |
| **SQLite / EF Core** | Banco de dados incorporado e camada de acesso a dados .NET. | Armazenam câmeras, configurações, eventos, revisão e auditoria. |
| **DPAPI** | Proteção de dados vinculada ao Windows e ao contexto do usuário. | Protege credenciais e origens sensíveis persistidas. |
| **MailKit / SMTP** | Biblioteca e protocolo de envio de e-mails. | Enviam notificações por meio da fila persistente, quando configuradas. |
| **Florence-2 (planejado)** | Modelo de visão da Microsoft com tarefas como localização associada a descrições textuais. | Candidato a detector local complementar para objetos como possíveis armas; integração e desempenho ainda precisam ser validados. |
| **Phrase grounding (planejado)** | Associação de uma descrição textual a regiões da imagem. | Poderá localizar objetos descritos como `handgun` ou `revolver`, sujeito à validação. |
| **Tracking temporal (planejado)** | Associação de objetos entre quadros sucessivos. | Base potencial para analisar corrida, aglomeração e mudanças de movimento, não disponível como identificação persistente de pessoas. |
| **Classificação de áudio (futura)** | Reconhecimento de eventos acústicos. | Poderá viabilizar alertas de gritos com captura e modelo de áudio próprios; Florence-2 não analisa áudio. |

## Direção do produto: templates de monitoramento

O objetivo é simplificar a configuração de alertas para operadores de supermercados, farmácias e outros estabelecimentos. Em vez de exigir ajustes de classes, IoU e janelas temporais, a interface poderá oferecer **templates selecionáveis por câmera**, com opções avançadas recolhidas. **Os templates abaixo são uma proposta de evolução, não capacidades já homologadas.**

| Template proposto | Abordagem técnica | Estado |
| --- | --- | --- |
| **Objetos conhecidos** | Classes já disponíveis no YOLOv8n e regras existentes. | Base técnica implementada; a nova interface de templates ainda é planejada. |
| **Possíveis armas de fogo e simulacros** | Avaliação de Florence-2 ou outro detector visual local, independente de intenção, profissão ou proximidade entre pessoas. | Experimental; reconhecimento de armas não validado. |
| **Facas e objetos cortantes** | Detecção de classes apropriadas, sujeita a testes de precisão. | Planejado; nenhuma capacidade especializada afirmada. |
| **Pessoas correndo** | Detecção de pessoas, tracking e deslocamento entre quadros. | Planejado; uma imagem isolada não demonstra corrida. |
| **Aglomeração anormal** | Contagem, densidade, áreas de interesse e variação ao longo do tempo. | Planejado; exige calibração por ambiente. |
| **Possível confronto físico** | Análise temporal e, se necessário, estimativa de pose ou reconhecimento de ações. | Pesquisa; proximidade de pessoas não comprova briga. |
| **Gritos / pedidos de socorro** | Captura de áudio e classificador acústico local. | Futuro; requer áudio e modelo independentes. |
| **Movimento fora do horário** | Análise de movimento e agenda configurável. | Proposta de template, dependente da implementação e testes. |

**Experiência desejada:** habilitar/desabilitar cada template, selecionar câmeras ou regiões de interesse e escolher a sensibilidade. O operador poderá revisar eventos e marcar falsos positivos. O sistema deve diferenciar **alerta preventivo** de **ocorrência confirmada**.

**Princípios:** inferência local sempre que viável, preservação do detector ONNX atual, modularidade, interface simples e avaliação real de falsos positivos, falsos negativos e latência antes de qualquer promessa comercial. O modo de detecção de armas deverá alertar por presença de objeto suspeito, inclusive parado, sem depender da identidade ou intenção de quem estiver na cena.

## Limitações e próximos passos

- Sem serviço Windows persistente, inicialização automática, watchdog ou garantia contínua.
- IoU não identifica pessoas persistentemente. Acurácia, segurança, falsos positivos e capacidade multicâmera não foram validados operacionalmente.
- Uma sessão local; sem MFA, recuperação de senha, controle por câmera, criptografia independente do banco/evidências ou auditoria inviolável. Proteja conta Windows, disco e dados.
- Evidências acompanham a pasta de dados; não há seletor independente. Alterar câmera/modelo reinicia todos os trabalhadores.
- Drivers nativos podem bloquear apesar do cancelamento; webcam e RTSP reais precisam de testes de campo.
- SMTP é de pelo menos uma entrega: queda após enviar e antes de registrar sucesso pode duplicar a mensagem. Unicidade evento/destinatário não elimina essa janela.
- A limpeza trata evidências registradas, não órfãs após queda de energia. Arquivamento da auditoria, vídeo contínuo, exportação, retenção legal, instalador assinado e atualização automática são futuros.
- O modelo declara AGPL-3.0. Revise pesos, imagem de teste, codecs e dependências antes de comercializar; não se afirma liberação comercial.

Antes do piloto: testes prolongados com hardware, revisão por operadores, validação de modelo/regras, privacidade/licenciamento, recuperação e empacotamento assinado. Host persistente exige testes próprios de ciclo de vida e autorização.

Teste da troca e persistência de idioma: `./scripts/Smoke-Localization.ps1`.
