Português (Brasil) | [English](en/architecture.md)

# Arquitetura

App.xaml.cs migra SQLite antes de iniciar Generic Host e abrir WPF. O fechamento aguarda o host; um mutex local impede processos simultâneos na mesma pasta.

| Projeto | Responsabilidade |
| --- | --- |
| Domain | Entidades, perfis, revisão/severidade, detecções e IoU |
| Application | Contratos, autorização, filtros, regras temporais e tradução |
| Infrastructure | EF/migrações, fluxos autorizados, hash, DPAPI, evidências, SMTP/fila, retenção e preferência de idioma |
| Monitoring | Manifesto/ONNX, fontes, captura/inferência, gerenciador e métricas |
| Desktop | WPF/MVVM, recursos visuais, bindings de tradução, imagens e registros JSON |

Infrastructure e Monitoring dependem de Application/Domain, nunca um do outro. IMonitoringStore conecta persistência em segundo plano. Cada operação cria/descarta seu DbContext; não há contexto entre threads, repositório genérico, backend, nuvem ou broker.

Pequenos fluxos EF permanecem junto da persistência. Autorização e regras não dependem de WPF. Code-behind cuida apenas da inicialização visual e fechamento. Senhas são lidas no comando e limpas dos controles.

Trabalhadores pertencem à configuração, não à navegação. Reutilizam ONNX e canal de um quadro que libera Mats antigos. Reaplicar câmera/modelo cancela e aguarda todos antes de recriar. Navegação, pausa, tela cheia e idioma não reiniciam câmeras. Detecções brutas ficam em memória; confirmações viram eventos. Envios entram na transação do evento e outro serviço processa lotes limitados.

WPF foi escolhido pelo escopo Windows/DPAPI. Recursos .resx têm português neutro e inglês satélite. A preferência não secreta language.json funciona antes de entrar. Consulte [internacionalização](internationalization.md).

## Host persistente futuro

Mova composição/monitoramento/infraestrutura/aplicação para serviço Windows. Use IPC local autenticado para prévias; defina conta, chaves DPAPI e permissões; teste atualização, recuperação e autorização. Só então fechar a interface poderá apenas desconectá-la. Esse host não está implementado.
