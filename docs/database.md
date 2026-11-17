Português (Brasil) | [English](en/database.md)

# Banco de dados e retenção

SQLite fica na pasta de dados. A migração InitialDesktop executa antes dos serviços; chaves estrangeiras são habilitadas e WAL é solicitado. Não há usuários, câmeras, regras ou incidentes fictícios inseridos automaticamente.

| Tabela | Conteúdo |
| --- | --- |
| Users | Nome único normalizado, hash Identity, perfil, ativação e bloqueio |
| Cameras | Nome/descrição/localização, tipo, origem protegida, habilitação e FPS |
| Rules | Classe/câmera, confiança/IoU, confirmações/janela/intervalo e severidade |
| Events | UTC, câmera/regra, classe/confiança/caixa, versão, revisão/anotações e evidência relativa |
| Recipients | Nome/endereço, habilitação e filtros |
| Deliveries | Par único evento/destinatário, mensagem, tentativas, próxima tentativa e resultado/erro |
| Settings | Configuração JSON, senha SMTP protegida |
| Audit | UTC, usuário e ação sem segredos |

Perfis são enums estáveis. Credenciais protegidas ficam na configuração. Cada evento contém uma detecção confirmada; não há histórico de todos os quadros ou vídeos BLOB. Catálogo de modelos e histórico de sessões são futuros. Índices cobrem data/câmera, revisão/severidade e envios/repetições; referências câmera/regra restringem exclusão. A interface permite desabilitar, sem excluir destrutivamente. Páginas têm até 100 linhas; volumes maiores podem exigir paginação por chave.

Retenção inicia junto do aplicativo e repete a cada seis horas, em lotes de 200. Padrão: 30 dias, ajustável de 1 a 3650. Remove imagens vinculadas, eventos expirados e envios. Arquivos são removidos antes das linhas para repetir falhas. Queda entre imagem e evento pode gerar órfãos; reconciliação é futura. Auditoria permanece e precisa de política de arquivamento.

## Migrações e backup

Instale dotnet-ef 10.0.15 em .tools. Use `.tools/dotnet-ef migrations add SuaMigracao --project src/SurveillanceAI.Infrastructure --startup-project src/SurveillanceAI.Infrastructure --output-dir Migrations`.

Com SDK local, defina DOTNET_ROOT e PATH. A fábrica de projeto usa surveillance-design.db separado. Não substitua migrações por EnsureCreated. Teste em cópia e faça backup de toda a pasta com o aplicativo fechado; copiar só o banco ativo pode perder WAL. EF não criptografa SQLite, que não é banco distribuído/de rede.

Idioma não muda esquema, enums, classes ou filtros persistidos. language.json fica fora das tabelas para funcionar antes da autenticação e não contém credenciais.
