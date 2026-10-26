# Graph Report - graphify  (2026-10-26)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 110 nodes · 184 edges · 10 communities (4 shown, 6 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ba30d70f`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Community 0
- Community 2
- Community 3
- Community 4
- Community 5
- Community 6
- Community 7
- Community 8
- Community 9

## God Nodes (most connected - your core abstractions)
1. `Enemy_Movement` - 14 edges
2. `ButtonHoverAnimation` - 13 edges
3. `FloatingUiTransition` - 12 edges
4. `MenuTransition` - 12 edges
5. `FloatingUI` - 11 edges
6. `PlayerMovement` - 9 edges
7. `DaggerHoverCycle` - 8 edges
8. `ToggleCheck` - 7 edges
9. `Enemy_Combat` - 6 edges
10. `PlayerHealth` - 6 edges

## Surprising Connections (you probably didn't know these)
- `Enemy_Movement` --references--> `LayerMask`  [EXTRACTED]
  critical/Assets/_Scripts/Enemy_Movement.cs →   _Bridges community 2 → community 0_
- `DaggerHoverCycle` --inherits--> `MonoBehaviour`  [EXTRACTED]
  critical/Assets/Menu/Scripts/DaggerHoverCycle.cs →   _Bridges community 3 → community 0_
- `ButtonHoverAnimation` --inherits--> `MonoBehaviour`  [EXTRACTED]
  critical/Assets/Menu/Scripts/ButtonHoverAnimation.cs →   _Bridges community 4 → community 0_
- `ButtonHoverAnimation` --references--> `Vector3`  [EXTRACTED]
  critical/Assets/Menu/Scripts/ButtonHoverAnimation.cs →   _Bridges community 4 → community 6_
- `FloatingUiTransition` --inherits--> `MonoBehaviour`  [EXTRACTED]
  critical/Assets/Menu/Scripts/FloatingUiTransition.cs →   _Bridges community 5 → community 0_

## Import Cycles
- None detected.

## Communities (10 total, 6 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.13
Nodes (3): Enemy_Combat, Player_Combat, PlayerHealth

### Community 2 - "Community 2"
Cohesion: 0.22
Nodes (5): Enemy_Movement, EnemyState, Attacking, Chasing, Idle

### Community 5 - "Community 5"
Cohesion: 0.25
Nodes (5): FloatingUiTransition, PosicaoBase, ModoFlutuar, Aleatorio, Vertical

### Community 7 - "Community 7"
Cohesion: 0.31
Nodes (4): FloatingUI, ModoFlutuar, Aleatorio, Vertical

## Knowledge Gaps
- **8 isolated node(s):** `Attacking`, `Chasing`, `Idle`, `PosicaoBase`, `Aleatorio` (+3 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 29 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Enemy_Movement` connect `Community 2` to `Community 0`, `Community 1`?**
  _High betweenness centrality (0.211) - this node is a cross-community bridge._
- **What connects `Attacking`, `Chasing`, `Idle` to the rest of the system?**
  _8 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._
- **Why does `ButtonHoverAnimation` connect `Community 4` to `Community 0`, `Community 1`, `Community 6`?**
  _High betweenness centrality (0.197) - this node is a cross-community bridge._
- **Why does `FloatingUiTransition` connect `Community 5` to `Community 0`, `Community 1`, `Community 3`, `Community 7`?**
  _High betweenness centrality (0.172) - this node is a cross-community bridge._