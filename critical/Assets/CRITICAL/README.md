# CRITICAL — organização do projeto

Os sistemas novos e a arte ficam em `Assets/CRITICAL`; pacotes externos ficam em `Assets/ThirdParty`. O menu autoral e os scripts originais mantêm seus caminhos em `Assets/Menu` e `Assets/_Scripts`.

| Pasta | Conteúdo |
| --- | --- |
| `Scenes` | `Menu` abre a partida em `Exploration`; `MainGame` preserva a cena anterior. |
| `Art` | Sprites, personagens, itens e tilesets da equipe. |
| `Gameplay/Runtime` | Encontros de sala e comportamentos de combate da demo. |
| `../_Scripts` | Movimento, vida, combate existentes e `Prefabs` de personagens, nos caminhos originais. |
| `UI/Runtime` | HUD, inventário, pausa e feedbacks de interface. |
| `../Menu` | Imagens e scripts do menu existente, nos caminhos originais. |
| `UI/Editor` | Instalação e ligação da interface nas cenas. |
| `UI/Prefabs` | Prefab do HUD da run; a hierarquia uGUI é criada pelo `GameHud`. |
| `Gameplay/Prefabs` | Prefab do rato usado nos encontros. |
| `Feedback/Runtime` | Arcos de ataque, partículas, números e sinais de impacto. |
| `Procedural` | `Runtime`, `Editor`, `Prefabs`, `Tiles` e `Animations` da geração de salas. |
| `Progression` | `Runtime`, `Items` e `Editor` dos itens, XP e upgrades. |
| `../ThirdParty/DungeonPack` | Assets importados do pacote de dungeon. |

Mova assets pelo Project da Unity ou por `AssetDatabase.MoveAsset`, sempre preservando os arquivos `.meta`. Os GUIDs mantêm as referências de cenas, prefabs, sprites e animações.

Os painéis, barras e efeitos geométricos feitos por código são **placeholders visuais**; a arte autoral existente continua sendo usada no menu, cenário, personagens e itens. Os valores iniciais de progressão e upgrades são ajustáveis para o balanceamento da demo.

## Jogar

Abra `Scenes/Menu.unity` e use Play. O botão Start carrega `Exploration` com fade.

| Controle | Ação |
| --- | --- |
| WASD / setas | Mover e orientar o golpe. |
| Espaço / J | Atacar na última direção de movimento. |
| Z / C | Selecionar o item anterior / próximo; a arma permanece no slot fixo. |
| Q | Usar o item selecionado; vida cheia não gasta a poção. |
| G | Soltar uma unidade do item selecionado no chão. |
| I / Tab | Abrir ou fechar o inventário. |
| Esc | Pausar; fechar inventário ou continuar. O botão Controles mostra todas as teclas. |
| Setas / Enter | Navegar e confirmar nos menus. |
| 1 / 2 / 3 | Escolher uma das melhorias de nível. |

Atravesse a porta de entrada para rolar o D20. A sala permanece selada até todos os inimigos serem derrotados; derrotas dão XP e ouro, e a sala limpa deixa um item no chão. Para coletar, basta passar por cima: nenhuma tecla de interação é necessária. Um item solto exige sair do seu alcance antes de poder recolhê-lo novamente. XP, inventário, ouro e melhorias são da run atual e reiniciam com Nova Run.

O HUD segue as referências fornecidas: círculos de vida, XP no topo, arma fixa, carrossel anterior/selecionado/próximo e D20 em losango. O ESC mostra o histórico das melhorias realmente escolhidas e a lista completa de controles. O menu principal usa a arte, a cena e os scripts do commit `9833dbc`; somente a ligação Start/Exit foi acrescentada.

## Ajustar conteúdo

Edite os cinco `ItemDefinition` em `Progression/Items`, ou crie um em **Create → CRITICAL → Item** e adicione ao catálogo `RunProgression.itemCatalog` no prefab `ExplorationPlayer`. Consumíveis gastam uma unidade; passivos aplicam seu bônus ao coletar. Os ícones usam a arte da equipe, com importação Single somente nas texturas que ainda não tinham nenhum recorte.

Os montadores em `UI/Editor` são ferramentas de instalação e organização; não é necessário executá-los para jogar. Eles recusam cenas com alterações não salvas. Não use **Build Exploration Scene** para atualizar o HUD: esse é o montador antigo do cenário procedural.

Os checks de progressão e geometria estão em **Tools → CRITICAL**; o smoke test completo começa em Play Mode a partir do Menu e salva relatórios em `Library/CRITICAL`. `Tools/Invoke-EditorCommand.ps1` permite executar a automação local sem mouse; ela só funciona quando o sentinel local já estiver habilitado.
