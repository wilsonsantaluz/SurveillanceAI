Português (Brasil) | [English](en/security.md)

# Segurança, privacidade e limites

## Autenticação

PasswordHasher Identity usa PBKDF2 com sal/versionamento e atualização de hash. Exige 12–256 caracteres; cinco falhas bloqueiam por cinco minutos. Não há senha de produção padrão nem acesso oculto. Novos usuários são Visualizadores; a configuração inicial cria Administrador. A biblioteca não cria servidor ASP.NET. [Orientação Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-10.0).

Session.Require verifica serviços: Administrador configura, Operador monitora/revisa, Visualizador consulta. Persistência em segundo plano continua após sair. Perfil/ativação valem no próximo acesso; há uma sessão por instalação. O administrador em uso não pode se desativar/rebaixar.

Administrador Windows, edição direta do banco ou malware na mesma conta contornam controles locais. Não há MFA, recuperação de senha, controle por câmera, identidade central ou auditoria inviolável. Autenticação local não isola o equipamento.

## Segredos e arquivos

Origem completa RTSP e senha SMTP usam DPAPI CurrentUser. PasswordBox é limpo após comandos; strings não garantem zeragem de memória. Servidor/usuário/remetente SMTP e destinatários permanecem legíveis. Recuperação exige perfil/chaves DPAPI original; outro usuário/máquina normalmente exige reinserir segredos. Senhas de usuários são hashes irreversíveis.

Dados herdam permissões do perfil. Banco, imagens e registros não têm criptografia própria. Use NTFS privado e criptografia de disco; proteja backup e evite pastas compartilhadas/sincronizadas/repositório. Evidências rejeitam caminhos absolutos, fuga da raiz e links/junções; isso não impede disputa de arquivos por administrador hostil.

Registros não contêm comandos EF, texto de rede, senhas, URLs ou dados por quadro. Diagnóstico nativo é silenciado. JSON retém 14 dias e aceita Informação/Atenção/Erro; auditoria exclui segredos. Códigos técnicos ficam estáveis e são traduzidos na apresentação.

## Evidências e LGPD

Imagens começam desabilitadas e só são salvas para confirmação de regra. Não há vídeo contínuo. Retenção de 30 dias/limpeza a cada seis horas é padrão do produto, não recomendação jurídica. E-mails começam desabilitados e levam metadados, não filmagens. Habilitar SMTP autoriza esse envio; teste administrativo envia uma mensagem explicitamente.

Antes de implantar: identifique controlador/operador, finalidade/base legal, minimize cobertura/retenção, defina acesso/exclusão e resposta a incidentes, forneça transparência e avalie transferências pelo provedor de e-mail. Obtenha revisão qualificada; o aplicativo não certifica LGPD. Veja [orientações/checklist ANPD](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia-orientativo-sobre-seguranca-da-informacao-para-agentes-de-tratamento-de-pequeno-porte).

## Comercialização

O modelo declara AGPL-3.0. Revise [Ultralytics](https://www.ultralytics.com/license), pesos/imagem, ONNX Runtime, OpenCvSharp/OpenCV/FFmpeg/codecs e dependências. Não se escolhe licença global nem se afirma liberação comercial.

Lançamento exige modelagem de ameaças, segurança/autorização, campo/modelo/regras, capacidade/retenção, privacidade, recuperação, licenças, instalador/assinatura e host persistente. Não anuncie crime previsto, agressão, armas, ameaça verificada ou serviço ininterrupto com base neste MVP.
