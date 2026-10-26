# Mapa de código do CRITICAL!

Gerado em 26/10/2026 com [Graphify 0.9.96](https://github.com/Graphify-Labs/graphify), Python 3.12 e extração AST local de C#. A skill `skill-codex.md` incluída no pacote foi consultada. Não foi instalada configuração global de assistente.

- [Abrir grafo interativo](graphify-out/graph.html)
- [Relatório gerado](graphify-out/GRAPH_REPORT.md)
- [Grafo consultável](graphify-out/graph.json)

Foram analisados os 12 scripts próprios de `critical/Assets/_Scripts` e `critical/Assets/Menu/Scripts`: **110 nós, 184 relações e 10 comunidades**. Documentos, imagens, PDF, pacotes, TextMesh Pro e código de terceiros ficaram fora da análise. Nenhuma API de modelo foi chamada.

O grafo mostra relações presentes no código. Ele **não valida o jogo rodando**, compilação, cenas, prefabs, referências do Inspector, colisões, Animation Events ou balanceamento. `EXTRACTED` significa extraído do código; não significa comportamento testado. A verificação estrutural encontrou zero referências de aresta ausentes, zero self-loops e zero duplicatas entre os dados exportados.

Começar a leitura por `Player_Combat`, `Enemy_Combat`, `PlayerHealth`, `PlayerMovement` e `Enemy_Movement`. As demais comunidades cobrem as animações e transições do menu. Os grupos foram calculados automaticamente e não definem uma nova arquitetura para o jogo.

## Reproduzir

Instale `graphifyy==0.9.96` em um ambiente Python isolado. O nome do pacote tem dois `y`; o comando é `graphify`. Na raiz do repositório, com esse ambiente ativo:

```powershell
graphify extract ./critical/Assets --code-only --out ./docs/graphify --exclude 'TextMesh Pro/**' --exclude 'Third Party/**' --exclude 'Plugins/**' --exclude '**/*.json' --max-workers 2
graphify cluster-only ./docs/graphify --no-label
graphify query 'Player_Combat PlayerHealth Enemy_Combat' --graph ./docs/graphify/graphify-out/graph.json --budget 1500
graphify diagnose multigraph --graph ./docs/graphify/graphify-out/graph.json
```

`--code-only` evita análise semântica remota; `--no-label` evita chamadas para nomear os grupos. A exportação JSON/HTML/Markdown aqui versionada usa referências de arquivo relativas à raiz do repositório. Em uma nova extração, o Graphify gera referências relativas a `critical/Assets`; anteponha `critical/Assets/` aos campos `source_file` e às mesmas referências no HTML/relatório antes de publicar. Mantenha os caches e manifestos locais ignorados pelo Git.

O relatório automático sugere `graphify update .`; para este mapa com saída fora de Assets, use os comandos completos acima. Refaça o mapa depois de alterar scripts: ele é um retrato da versão identificada em `built_at_commit`, não um monitor contínuo.
