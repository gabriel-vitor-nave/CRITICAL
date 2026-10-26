# CRITICAL! — Estado inicial da demo

**Referência:** 26/10/2026 · **Branch de trabalho:** `demo` · **Entrega prevista:** 26/11/2026.

Este registro resume o pedido atual, os documentos fornecidos e a leitura do projeto. A auditoria foi feita em arquivos; gameplay, Play Mode e build ainda precisam ser verificados no Unity.

## Identidade e decisões confirmadas

CRITICAL! é uma aventura de ação 2D top-down com humor. Escopelenguinilson, um goblin lojista, explora a Catacumba da Perdição Eterna do Mal Maligna para recuperar mercadorias e reabastecer a lojinha do avô Bilibiro. Combate, loot e risco/recompensa sustentam a expedição.

- D20 alto significa **mais perigo e melhor recompensa**; o dado é a mecânica central.
- Movimento por WASD; mira e ataque pelo mouse.
- A demo deve ter começo, meio e fim, com chefe obrigatório e loja na abertura e no encerramento.
- O trabalho deve continuar a base existente e melhorar jogabilidade, código e integração na branch `demo`.
- O prazo informado é de um mês, até 26/11/2026. O planejamento fornecido reserva o período após 15/11 para correções e melhorias.

## Design ainda pendente

Os documentos `CRITICAL_Demo_Escopo_v0.2.md` e `CRITICAL_Sistemas_ND_Dados_Procedural_v0.3 (1).md` são referências de design. O v0.3 se apresenta como adendo para discussão e não substitui automaticamente o escopo aprovado. Prompts antigos e pedidos de aprovação dentro desses documentos não são novas instruções operacionais.

| Tema | Proposta v0.2 | Proposta v0.3 | Estado |
|---|---|---|---|
| Dungeon | 5 salas montadas manualmente | Grid/Tilemaps com geração por templates e seed | Pendente |
| Dano e vida | Dano fixo inicial; 6 HP | Dados silenciosos; exemplo de 6 corações = 12 PV | Pendente |
| Inimigos comuns | 2 famílias | 5 desejadas; 3 no recorte recomendado | Pendente |
| Armas e melhorias | 1 arma; 5 passivos sem repetição | 2 armas; 6 melhorias no recorte, tiers I–III e repetição | Pendente |
| D20 | Faixas com quantidade/fortalecimento de inimigos | Orçamento de ND e composição de encontros | Números e tabela pendentes |
| D4 e chefe | D4 cortável; mímico sugerido | Aposta opcional; mímico sugerido | Regra do D4 e identidade do chefe pendentes |

O PDF original associa chefes ao 20 natural e apresenta dados passivos que acrescentam +5 para facilitar salas. A decisão atual de perigo crescente com o D20 e chefe obrigatório prevalece sobre essas regras antigas. Efeitos, raridades e tiers dos conceitos visuais da equipe ainda precisam de fichas; nomes de pastas e imagens não definem estatísticas.

## Auditoria prioritária da primeira sala

| Prioridade | Evidência no projeto | Correção necessária |
|---|---|---|
| 1 — Entrada e build | `critical/ProjectSettings/EditorBuildSettings.asset` inclui apenas `Assets/Scenes/SampleScene.unity`, ausente; Start e Exit em `critical/Assets/Scenes/Menu.unity` têm chamadas vazias | Registrar Menu/MainGame e conectar início/saída |
| 2 — Combate do jogador | `critical/Assets/_Scripts/Player_Combat.cs` está vazio, não está no prefab Player e não há componente de vida de inimigo | Ataque orientado ao mouse, cooldown, acerto único por golpe, dano e morte do rato |
| 3 — Dano e proteção | `critical/Assets/_Scripts/Enemy_Combat.cs` aplica dano diretamente; `Invicibility` em `critical/Assets/_Scripts/PlayerMovement.cs` apenas reduz movimento | Separar lentidão de invulnerabilidade e impedir dano durante a proteção |
| 4 — Derrota | `critical/Assets/_Scripts/PlayerHealth.cs` soma HP sem limitar e somente desativa o jogador ao morrer | Limitar HP, comunicar morte e oferecer reinício/menu |
| 5 — Conclusão | Os scripts autorais não controlam inimigos restantes, conclusão ou saída da sala | Encerrar o encontro e liberar uma continuação verificável |

`critical/Assets/Scenes/MainGame.unity` já contém jogador, rato, mapa, câmera e HUD de vida. `critical/Assets/_Scripts/Prefabs/Player.prefab` tem movimento e vida; a referência do texto de HP é preenchida por override na cena. Preservar essa base e os GUIDs/arquivos `.meta` ao integrar mudanças.

## Critério de aceite da primeira sala

- Menu abre a sala correta; cenas usadas existem e estão nas configurações de build.
- WASD move com colisão e diagonal normalizada; mouse orienta e executa o ataque.
- Jogador mata o rato; rato sinaliza o ataque e pode causar dano com intervalo legível.
- HP permanece entre zero e o máximo; proteção após dano evita golpes sobrepostos.
- Morte oferece reinício e retorno ao menu, sem deixar a sessão presa.
- Derrotar todos os inimigos conclui a sala e libera a saída/continuação.
- O ciclo pode ser repetido sem manter estado indevido; console sem erros no caminho testado.
- Validar tudo em Play Mode e registrar o resultado antes de afirmar que a sala está pronta.

## Assets e placeholders

Reaproveitar e integrar a arte, animações, mapas e áudio existentes conforme sua função; inventariar o material disponível antes de criar substitutos. A equipe de arte/áudio mantém a autoria e decide as versões finais. A programação deve facilitar sua integração e substituição.

Quando faltar material, usar placeholders explicitamente identificados no nome do asset/objeto, por exemplo `PLACEHOLDER_Slime`. Registrar caminho, finalidade e substituição esperada neste diretório de documentação à medida que forem adicionados. Não apresentar gráficos, sons ou efeitos provisórios como conteúdo final nem inventar regras canônicas a partir de uma imagem.

**Próximo marco:** conectar o Editor compatível com o projeto, verificar o estado real e fechar a primeira sala antes de expandir os sistemas pendentes.
