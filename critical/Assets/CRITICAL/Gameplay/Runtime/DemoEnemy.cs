using System;
using UnityEngine;
using Critical.Feedback;
using Critical.Progression;
using Critical.UI;
using Critical.Procedural;

namespace Critical.Gameplay
{
    public sealed class DemoEnemy : MonoBehaviour
    {
        public int maxHealth = 5;
        public float speed = 1.1f;
        public Sprite[] idleFrames, walkFrames, attackFrames;
        public bool IsAlive => health > 0;
        public event Action<DemoEnemy> Defeated;
        private int health;
        private PlayerHealth target;
        private Rigidbody2D body;
        private Collider2D bodyCollider, targetCollider;
        private ExplorationFlow flow;
        private SpriteRenderer sprite;
        private Vector3 baseScale;
        private Vector2 knockback;
        private float attackAt, nextAttack, flashUntil;
        private bool winding;
        private float nextPlan;
        private RoomLayout plannedRoom;
        private Vector2Int plannedTarget;
        private int[] frontier, parents;
        private Vector2Int[] path;
        private int pathLength, pathIndex;
        private static readonly Vector2Int[] Neighbors =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            sprite = GetComponentInChildren<SpriteRenderer>();
            baseScale = sprite.transform.localScale;
            health = Mathf.Max(1, maxHealth);
        }

        public void Configure(PlayerHealth player, int risk)
        {
            target = player;
            targetCollider = player != null ? player.GetComponent<Collider2D>() : null;
            flow = FindObjectOfType<ExplorationFlow>();
            maxHealth = 4 + risk / 5;
            health = maxHealth;
            speed = 0.9f + risk * 0.025f;
        }

        private void Update()
        {
            if (!IsAlive || target == null || target.IsDead || GameHud.IsInputBlocked || Time.timeScale <= 0f) return;
            Vector2 origin = Center;
            Vector2 destination = TargetCenter;
            Vector2 delta = destination - origin;
            sprite.flipX = delta.x < 0f;
            if (winding && Time.time >= attackAt)
            {
                winding = false;
                nextAttack = Time.time + 0.8f;
                if (delta.sqrMagnitude < 1.25f && CombatVisibility.IsClear(origin, destination)) target.ChangeHP(-1);
            }
            if (!winding && delta.sqrMagnitude < 1f && Time.time >= nextAttack && CombatVisibility.IsClear(origin, destination))
            {
                winding = true;
                attackAt = Time.time + 0.4f;
            }
            Sprite[] frames = winding ? attackFrames : delta.sqrMagnitude > 0.8f ? walkFrames : idleFrames;
            if (frames != null && frames.Length > 0) sprite.sprite = frames[(int)(Time.time * 9f) % frames.Length];
            float punch = Mathf.Max(0f, (flashUntil - Time.time) / 0.15f);
            sprite.color = punch > 0f ? Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), punch)
                : winding ? new Color(1f, 0.48f + Mathf.Sin(Time.time * 28f) * 0.2f, 0.35f) : Color.white;
            sprite.transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + punch * 0.3f, 1f - punch * 0.22f, 1f));
        }

        private void FixedUpdate()
        {
            if (!IsAlive || target == null || target.IsDead || GameHud.IsInputBlocked) { body.velocity = Vector2.zero; return; }
            Vector2 origin = Center;
            Vector2 destination = TargetCenter;
            Vector2 offset = destination - origin;
            bool visible = CombatVisibility.IsClear(origin, destination);
            Vector2 desired = !winding && (offset.sqrMagnitude > 0.7f || !visible)
                ? Navigate(origin, destination) * speed : Vector2.zero;
            body.velocity = desired + knockback;
            knockback = Vector2.MoveTowards(knockback, Vector2.zero, Time.fixedDeltaTime * 22f);
        }

        private Vector2 Center => bodyCollider != null ? (Vector2)bodyCollider.bounds.center : body.position;
        private Vector2 TargetCenter => targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)target.transform.position;

        private Vector2 Navigate(Vector2 origin, Vector2 destination)
        {
            float radius = bodyCollider != null ? Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) : 0.22f;
            if (CombatVisibility.HasClearance(origin, destination, radius)) return (destination - origin).normalized;
            if (flow == null || flow.CurrentRoom == null || flow.RoomView == null || flow.RoomView.ground == null) return Vector2.zero;
            Vector3Int startCell = flow.RoomView.ground.WorldToCell(origin);
            Vector3Int targetCell = flow.RoomView.ground.WorldToCell(destination);
            var start = new Vector2Int(startCell.x, startCell.y);
            var goal = new Vector2Int(targetCell.x, targetCell.y);
            if (plannedRoom != flow.CurrentRoom || plannedTarget != goal || Time.time >= nextPlan)
                PlanPath(flow.CurrentRoom, start, goal);
            while (pathIndex < pathLength)
            {
                Vector2 waypoint = flow.RoomView.CellCenter(path[pathIndex]);
                Vector2 offset = waypoint - origin;
                if (offset.sqrMagnitude > 0.01f) return offset.normalized;
                pathIndex++;
            }
            return Vector2.zero;
        }

        private void PlanPath(RoomLayout layout, Vector2Int start, Vector2Int goal)
        {
            // Finish the current cell-to-cell segment when replanning. Skipping its center
            // on every timer tick would cut tight corners against the real colliders.
            bool finishSegment = plannedRoom == layout && pathIndex < pathLength &&
                (path[pathIndex] - start).sqrMagnitude <= 2;
            if (finishSegment) start = path[pathIndex];
            plannedRoom = layout;
            plannedTarget = goal;
            nextPlan = Time.time + 0.4f;
            pathLength = pathIndex = 0;
            if (!layout.IsWalkable(start.x, start.y) || !layout.IsWalkable(goal.x, goal.y)) return;
            int size = layout.Width * layout.Height;
            if (parents == null || parents.Length < size)
            {
                parents = new int[size];
                frontier = new int[size];
                path = new Vector2Int[size];
            }
            for (int i = 0; i < size; i++) parents[i] = -1;
            int first = start.y * layout.Width + start.x;
            int last = goal.y * layout.Width + goal.x;
            int head = 0, tail = 1;
            frontier[0] = first;
            parents[first] = first;
            while (head < tail && parents[last] < 0)
            {
                int current = frontier[head++];
                var cell = new Vector2Int(current % layout.Width, current / layout.Width);
                foreach (Vector2Int step in Neighbors)
                {
                    Vector2Int neighbor = cell + step;
                    if (!layout.IsWalkable(neighbor.x, neighbor.y)) continue;
                    int index = neighbor.y * layout.Width + neighbor.x;
                    if (parents[index] >= 0) continue;
                    parents[index] = current;
                    frontier[tail++] = index;
                }
            }
            if (parents[last] < 0) return;
            for (int cursor = last; cursor != first; cursor = parents[cursor])
                path[pathLength++] = new Vector2Int(cursor % layout.Width, cursor / layout.Width);
            if (finishSegment) path[pathLength++] = start;
            System.Array.Reverse(path, 0, pathLength);
        }

        public void TakeDamage(int amount, Vector2 direction, bool critical = false)
        {
            if (!IsAlive || amount <= 0) return;
            health = Mathf.Max(0, health - amount);
            flashUntil = Time.time + 0.15f;
            knockback = direction.normalized * 4f;
            winding = false;
            nextAttack = Time.time + 0.55f;
            Color tint = critical ? new Color(1f, 0.81f, 0.25f) : Color.white;
            WorldFeedback.Number(transform.position + Vector3.up * 0.7f, critical ? amount + "!" : amount.ToString(), tint);
            WorldFeedback.Burst(transform.position, tint, critical ? 14 : 7);
            var flow = FindObjectOfType<Critical.Procedural.ExplorationFlow>();
            if (flow != null) flow.Shake(critical ? 0.15f : 0.07f);
            if (IsAlive) return;
            GetComponent<Collider2D>().enabled = false;
            body.velocity = Vector2.zero;
            body.simulated = false;
            var progression = RunProgression.Instance;
            if (progression != null)
            {
                progression.AddGold(3);
                progression.GrantExperience(12);
            }
            Defeated?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
