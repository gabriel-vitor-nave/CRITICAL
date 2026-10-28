using UnityEngine;
using Critical.Feedback;
using Critical.Procedural;
using Critical.Progression;
using Critical.UI;

namespace Critical.Gameplay
{
    public sealed class WorldItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private int count = 1;
        [SerializeField] private bool wasDropped, waitingForOwnerExit;
        [SerializeField] private RunProgression droppedOwner;
        [SerializeField] private ExplorationFlow sourceFlow;
        [SerializeField] private int roomIndex;
        [SerializeField] private float collectAfter;
        private CircleCollider2D trigger;
        private SpriteRenderer icon;
        private Transform ring;
        private Collider2D ownerCollider;
        private float nextAttempt;
        private bool collecting;
        private static Sprite placeholder;
        private static Material unlit;
        private static readonly Collider2D[] PlacementHits = new Collider2D[16];

        public ItemDefinition Item => item;
        public int Count => count;
        public bool WasDropped => wasDropped;
        public bool RequiresOwnerExit => waitingForOwnerExit;

        public static WorldItemPickup Spawn(ItemDefinition item, int count, Vector3 position,
            bool dropped = false, RunProgression droppedOwner = null)
        {
            if (item == null || string.IsNullOrEmpty(item.Id) || count <= 0 || count > RunProgression.MaximumStack) return null;
            var root = new GameObject("Pickup_" + item.Id);
            root.transform.position = position;
            var trigger = root.AddComponent<CircleCollider2D>();
            trigger.radius = 0.42f;
            trigger.isTrigger = true;
            var visual = new GameObject(item.Icon != null ? "ItemIcon" : "PLACEHOLDER_ItemIcon");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = item.Icon != null ? item.Icon : Placeholder;
            renderer.color = item.Icon != null ? Color.white : RunProgression.RarityColor(item.Rarity);
            renderer.sharedMaterial = Unlit;
            renderer.sortingOrder = 7;
            float size = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
            visual.transform.localScale = Vector3.one * (0.55f / Mathf.Max(size, 0.01f));
            var ringObject = new GameObject("PickupRing");
            ringObject.transform.SetParent(root.transform, false);
            var line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 25;
            line.startWidth = line.endWidth = 0.022f;
            line.sharedMaterial = Unlit;
            line.sortingOrder = 6;
            Color tint = RunProgression.RarityColor(item.Rarity);
            tint.a = 0.65f;
            line.startColor = line.endColor = tint;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i / 24f * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.32f, Mathf.Sin(angle) * 0.15f, 0));
            }
            var pickup = root.AddComponent<WorldItemPickup>();
            pickup.item = item;
            pickup.count = count;
            pickup.wasDropped = dropped;
            pickup.droppedOwner = droppedOwner;
            pickup.ownerCollider = droppedOwner != null ? droppedOwner.GetComponent<Collider2D>() : null;
            pickup.waitingForOwnerExit = dropped && droppedOwner != null;
            pickup.collectAfter = Time.time + (dropped ? 0.35f : 0.15f);
            pickup.sourceFlow = FindObjectOfType<ExplorationFlow>();
            pickup.roomIndex = pickup.sourceFlow != null ? pickup.sourceFlow.CurrentRoomIndex : 0;
            pickup.trigger = trigger;
            pickup.icon = renderer;
            pickup.ring = ringObject.transform;
            Physics2D.SyncTransforms();
            if (pickup.waitingForOwnerExit && pickup.ownerCollider != null
                && !trigger.Distance(pickup.ownerCollider).isOverlapped) pickup.waitingForOwnerExit = false;
            return pickup;
        }

        public static WorldItemPickup SpawnDropped(RunProgression owner, ItemDefinition item, int count = 1)
        {
            if (owner == null || !owner.isActiveAndEnabled) return null;
            var health = owner.GetComponent<PlayerHealth>();
            var body = owner.GetComponent<Rigidbody2D>();
            var collider = owner.GetComponent<Collider2D>();
            if (health == null || health.IsDead || body == null || !body.simulated || collider == null) return null;
            var flow = FindObjectOfType<ExplorationFlow>();
            if (flow != null && flow.IsTransitioning) return null;
            Vector3 position = collider.bounds.center;
            if (flow != null && flow.CurrentRoom != null && flow.RoomView != null && flow.RoomView.ground != null)
            {
                Vector3Int cell = flow.RoomView.ground.WorldToCell(position);
                bool found = false;
                float bestDistance = float.PositiveInfinity;
                Vector3 best = position;
                for (int y = cell.y - 2; y <= cell.y + 2; y++)
                    for (int x = cell.x - 2; x <= cell.x + 2; x++)
                    {
                        if (!flow.CurrentRoom.InBounds(x, y) || flow.CurrentRoom[x, y] != RoomCell.Floor) continue;
                        Vector3 candidate = flow.RoomView.CellCenter(new Vector2Int(x, y));
                        float distance = (candidate - position).sqrMagnitude;
                        if (distance >= bestDistance || !ClearPlacement(candidate)) continue;
                        bestDistance = distance;
                        best = candidate;
                        found = true;
                    }
                if (!found) return null;
                position = best;
            }
            else if (!ClearPlacement(position)) return null;
            position.z = 0;
            return Spawn(item, count, position, true, owner);
        }

        public static bool TrySpawnDropped(ItemDefinition item, int count, Vector3 position, RunProgression owner = null) =>
            SpawnDropped(owner != null ? owner : RunProgression.Instance, item, count) != null;

        public static void ClearPreviousRoom(ExplorationFlow flow)
        {
            foreach (WorldItemPickup pickup in FindObjectsOfType<WorldItemPickup>())
                if (pickup.sourceFlow == flow && pickup.roomIndex != flow.CurrentRoomIndex) Destroy(pickup.gameObject);
        }

        private void Awake() => BindComponents();
        private void OnEnable() => BindComponents();

        private void BindComponents()
        {
            if (trigger == null) trigger = GetComponent<CircleCollider2D>();
            if (icon == null) icon = GetComponentInChildren<SpriteRenderer>();
            if (ring == null) ring = transform.Find("PickupRing");
            if (ownerCollider == null && droppedOwner != null) ownerCollider = droppedOwner.GetComponent<Collider2D>();
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * 4f + transform.position.x);
            if (icon != null) icon.transform.localPosition = new Vector3(0, 0.23f + wave * 0.055f, 0);
            if (ring != null) ring.localScale = Vector3.one * (1f + wave * 0.06f);
            UpdateExitGate();
        }

        private void UpdateExitGate()
        {
            if (!waitingForOwnerExit || trigger == null) return;
            if (ownerCollider == null || !ownerCollider.enabled || !ownerCollider.gameObject.activeInHierarchy
                || !trigger.Distance(ownerCollider).isOverlapped) waitingForOwnerExit = false;
        }

        public bool TryCollect(Collider2D other)
        {
            if (collecting || item == null || count <= 0 || trigger == null || other == null
                || !other.enabled || !other.gameObject.activeInHierarchy || !isActiveAndEnabled
                || Time.timeScale <= 0f || Time.time < collectAfter || Time.time < nextAttempt || GameHud.IsInputBlocked)
                return false;
            var run = other.GetComponentInParent<RunProgression>();
            if (run == null || !run.isActiveAndEnabled || !trigger.Distance(other).isOverlapped) return false;
            var health = run.GetComponent<PlayerHealth>();
            var body = run.GetComponent<Rigidbody2D>();
            if (health == null || health.IsDead || body == null || !body.simulated || (sourceFlow != null && sourceFlow.IsTransitioning)) return false;
            UpdateExitGate();
            if (waitingForOwnerExit && run == droppedOwner) return false;
            collecting = true;
            if (!run.AddItem(item, count, restoreDroppedItem: wasDropped))
            {
                collecting = false;
                nextAttempt = Time.time + 1f;
                return false;
            }
            trigger.enabled = false;
            WorldFeedback.Burst(transform.position, RunProgression.RarityColor(item.Rarity), 8);
            Destroy(gameObject);
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);
        private void OnTriggerExit2D(Collider2D other) => UpdateExitGate();

        private static bool ClearPlacement(Vector2 position)
        {
            int found = Physics2D.OverlapCircleNonAlloc(position, 0.25f, PlacementHits);
            for (int i = 0; i < found; i++)
            {
                Collider2D collider = PlacementHits[i];
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<PlayerMovement>() != null
                    || collider.GetComponentInParent<DemoEnemy>() != null) continue;
                return false;
            }
            return found < PlacementHits.Length;
        }

        private static Material Unlit
        {
            get
            {
                if (unlit == null) unlit = new Material(Shader.Find("Sprites/Default"));
                return unlit;
            }
        }

        private static Sprite Placeholder
        {
            get
            {
                if (placeholder == null)
                    placeholder = Sprite.Create(Texture2D.whiteTexture,
                        new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f, Texture2D.whiteTexture.width);
                return placeholder;
            }
        }
    }
}
