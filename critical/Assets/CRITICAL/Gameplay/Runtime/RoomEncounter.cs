using System.Collections.Generic;
using UnityEngine;
using Critical.Procedural;
using Critical.Progression;
using Critical.Feedback;
using Critical.UI;

namespace Critical.Gameplay
{
    public sealed class RoomEncounter : MonoBehaviour
    {
        public ExplorationFlow flow;
        public DemoEnemy enemyPrefab;
        public int EnemiesRemaining => enemies.Count;
        private readonly List<DemoEnemy> enemies = new List<DemoEnemy>();
        private bool rewarded;
        private Vector3 lastDefeatedPosition;

        private void OnEnable()
        {
            if (flow == null) flow = GetComponent<ExplorationFlow>();
            if (flow == null) return;
            flow.RoomGenerated += Prepare;
            flow.RoomRevealed += Begin;
        }

        private void Prepare(RoomLayout layout)
        {
            rewarded = false;
            foreach (DemoEnemy enemy in enemies) if (enemy != null) Destroy(enemy.gameObject);
            enemies.Clear();
            WorldItemPickup.ClearPreviousRoom(flow);
        }

        private void Begin(RoomLayout layout)
        {
            if (enemyPrefab == null) { Complete(); return; }
            var random = new System.Random(layout.Seed ^ 5831);
            var candidates = new List<Vector2Int>();
            for (int y = 2; y < layout.Height - 2; y++)
                for (int x = 2; x < layout.Width - 2; x++)
                {
                    if (layout[x, y] != RoomCell.Floor) continue;
                    Vector2Int cell = new Vector2Int(x, y);
                    if ((cell - layout.Entry.InnerCell).sqrMagnitude < 25 || (cell - layout.Exit.InnerCell).sqrMagnitude < 9) continue;
                    candidates.Add(cell);
                }
            int count = Mathf.Min(1 + layout.Roll / 5, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int selected = random.Next(candidates.Count);
                Vector3 position = flow.RoomView.CellCenter(candidates[selected]);
                candidates.RemoveAt(selected);
                var enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
                enemy.Configure(flow.player.GetComponent<PlayerHealth>(), layout.Roll);
                enemy.Defeated += OnDefeated;
                enemies.Add(enemy);
                WorldFeedback.Burst(position, new Color(0.7f, 0.4f, 0.9f), 8);
            }
            if (GameHud.Instance != null) GameHud.Instance.ShowToast("ELIMINE " + enemies.Count + " AMEAÇAS • A SAÍDA ESTÁ SELADA", new Color(1f, 0.78f, 0.38f));
            if (enemies.Count == 0) Complete();
        }

        private void OnDefeated(DemoEnemy enemy)
        {
            lastDefeatedPosition = enemy.transform.position;
            enemy.Defeated -= OnDefeated;
            enemies.Remove(enemy);
            if (enemies.Count == 0) Complete();
        }

        private void Complete()
        {
            if (rewarded) return;
            rewarded = true;
            var progression = RunProgression.Instance;
            int roll = flow.CurrentRoom != null ? flow.CurrentRoom.Roll : 1;
            if (progression != null)
            {
                progression.AddGold(5 + roll);
                // Items stay on the floor until the player walks over them.
                if (progression.Catalog.Count > 0)
                {
                    int index = (flow.CurrentRoomIndex - 1) % progression.Catalog.Count;
                    Vector3 position = LootPosition();
                    WorldItemPickup.Spawn(progression.Catalog[Mathf.Max(0, index)], 1, position);
                }
                progression.GrantExperience(18 + roll);
            }
            flow.OpenExit();
            if (GameHud.Instance != null) GameHud.Instance.ShowToast("SALA LIMPA!  SAÍDA LIBERADA", new Color(0.5f, 1f, 0.7f));
        }

        private Vector3 LootPosition()
        {
            if (flow.CurrentRoom == null || flow.RoomView == null) return lastDefeatedPosition;
            var layout = flow.CurrentRoom;
            Vector3 center = flow.RoomView.CellCenter(new Vector2Int(layout.Width / 2, layout.Height / 2));
            Collider2D playerCollider = flow.player != null ? flow.player.GetComponent<Collider2D>() : null;
            Vector3 playerPosition = playerCollider != null ? playerCollider.bounds.center : center;
            Vector3 best = lastDefeatedPosition;
            float distance = float.PositiveInfinity;
            for (int y = 1; y < layout.Height - 1; y++)
                for (int x = 1; x < layout.Width - 1; x++)
                {
                    if (layout[x, y] != RoomCell.Floor) continue;
                    Vector3 position = flow.RoomView.CellCenter(new Vector2Int(x, y));
                    if (playerCollider != null && (position - playerPosition).sqrMagnitude < 1.44f) continue;
                    float candidate = (position - center).sqrMagnitude;
                    if (candidate >= distance) continue;
                    best = position;
                    distance = candidate;
                }
            return best;
        }

        private void OnDisable()
        {
            if (flow == null) return;
            flow.RoomGenerated -= Prepare;
            flow.RoomRevealed -= Begin;
        }
    }
}
