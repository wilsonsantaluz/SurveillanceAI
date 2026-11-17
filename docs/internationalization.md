Português (Brasil) | [English](en/internationalization.md)

# Internacionalização

Português do Brasil (pt-BR) é padrão independentemente do Windows. O seletor Idioma / Language funciona antes/depois de entrar e troca imediatamente para en-US sem reiniciar monitoramento.

## Implementação

- Application/Localization/Strings.resx: português neutro; Strings.en.resx: inglês em assembly satélite incluído na publicação.
- Texts: recursos, formatos e notificações LanguageTag/Item[]; TrExtension: bindings WPF vivos.
- LocalizedValueConverter: apresentação de enums, situações e auditoria sem alterar dados.
- Infrastructure/LanguagePreferences: language.json não secreto na raiz de dados, com gravação atômica; ausente/inválido/desconhecido usa português.

Chaves derivam do SHA-256 do texto inglês; comentários identificam cada texto. Mantenha a mesma chave e os marcadores {0}/{1}/formatos nas duas versões. Acrescente ambos os recursos ao criar textos e execute os testes de paridade/cultura.

Datas/números usam a cultura selecionada (vírgula decimal no português, ponto no inglês). Horários internos continuam UTC; os cabeçalhos indicam UTC. Enums persistidos, modos SMTP, nomes de projetos/classes do modelo (person, bus) e integrações não são traduzidos. Dados digitados pelo usuário não são traduzidos automaticamente.

Validações e conteúdo de e-mail usam recursos. Logs técnicos mantêm códigos estáveis. Mensagem já enfileirada mantém o idioma de criação; mudar UI não reescreve histórico. Idioma é preferência por instalação/conta Windows, disponível a qualquer perfil porque não altera monitoramento/segredos.

## Documentação

readme.md e docs são portugueses; readme.en.md e docs/en são ingleses. Atualize ambos quando mudar comportamento e preserve comandos, caminhos e nomes de configuração.

Teste da troca e persistência de idioma: `./scripts/Smoke-Localization.ps1`.
