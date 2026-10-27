using System;
using System.Collections;
using UnityEngine;

namespace Critical.Procedural
{
    public sealed class ExplorationFlow : MonoBehaviour
    {
        public PlayerMovement player;
        public Rigidbody2D playerBody;
        public new Camera camera;
        public RoomTilemapView roomPrefab;
        public CatacombDoor doorPrefab;
        public CatacombDoor outsideDoor;
        public GameObject outsideArea;
        public GameObject darknessOverlay;
        public CanvasGroup overlayGroup;
        public DiceRoller diceRoller;
        public GameObject playerLight, roomLight;
        public TMPro.TMP_Text runStatsPlaceholder;
        public RoomGenerationSettings generation = new RoomGenerationSettings();
        [Tooltip("Zero sorteia uma seed para a run; outro valor permite reproduzi-la.")]
        public int runSeed;
        [Min(0f)] public float revealDuration = 0.4f;

        public int CurrentSeed { get; private set; }
        public int CurrentRoomIndex { get; private set; }
        public int CurrentRoomSeed { get; private set; }
        public RoomLayout CurrentRoom { get; private set; }
        public bool IsTransitioning { get; private set; }
        public event Action<RoomLayout> RoomGenerated;
        public event Action<RoomLayout> RoomRevealed;

        private System.Random random;
        private RoomTilemapView room;
        private CatacombDoor entry, exit;
        private DoorDirection nextEntry = DoorDirection.South;

        private void Start()
        {
            if (player == null || playerBody == null || camera == null || roomPrefab == null || doorPrefab == null
                || outsideDoor == null || outsideArea == null || darknessOverlay == null || overlayGroup == null
                || diceRoller == null || playerLight == null || roomLight == null)
            {
                enabled = false;
                Debug.LogError("ExplorationFlow: há referências obrigatórias ausentes no Inspector.", this);
                return;
            }

            CurrentSeed = runSeed == 0 ? Environment.TickCount : runSeed;
            random = new System.Random(CurrentSeed);
            darknessOverlay.SetActive(false);
            overlayGroup.alpha = 1f;
            outsideArea.SetActive(true);
            playerLight.SetActive(true);
            roomLight.SetActive(true);
            if (runStatsPlaceholder != null)
                runStatsPlaceholder.text = "ESTATÍSTICAS DE RUNS\nEspaço reservado";
            outsideDoor.Configure(new DoorPlacement(Vector2Int.zero, DoorDirection.North), false);
            outsideDoor.Traversed += OnDoorTraversed;
            StartCoroutine(outsideDoor.Open());
        }

        private void LateUpdate()
        {
            if (camera == null || player == null) return;
            Vector3 position = player.transform.position;
            camera.transform.position = new Vector3(position.x, position.y, camera.transform.position.z);
        }

        private void OnDoorTraversed(CatacombDoor door)
        {
            if (IsTransitioning || (door != outsideDoor && door != exit)) return;
            if (door == exit) nextEntry = (DoorDirection)(((int)door.Direction + 2) % 4);
            StartCoroutine(GenerateNextRoom());
        }

        private IEnumerator GenerateNextRoom()
        {
            IsTransitioning = true;
            player.enabled = false;
            playerBody.velocity = Vector2.zero;
            playerBody.simulated = false;
            darknessOverlay.SetActive(true);
            overlayGroup.alpha = 1f;
            playerLight.SetActive(false);
            roomLight.SetActive(false);
            outsideArea.SetActive(false);
            outsideDoor.gameObject.SetActive(false);
            if (entry != null) entry.gameObject.SetActive(false);
            if (exit != null) exit.gameObject.SetActive(false);
            if (room != null) room.Clear();
            CurrentRoom = null;

            yield return diceRoller.Roll(random);

            CurrentRoomSeed = random.Next();
            CurrentRoom = RoomGenerator.Generate(generation, CurrentRoomSeed, diceRoller.LastRoll, nextEntry);
            if (room == null) room = Instantiate(roomPrefab);
            room.gameObject.SetActive(true);
            room.Build(CurrentRoom);
            if (entry == null) entry = Instantiate(doorPrefab);
            if (exit == null)
            {
                exit = Instantiate(doorPrefab);
                exit.Traversed += OnDoorTraversed;
            }
            ConfigureDoor(entry, CurrentRoom.Entry, true);
            ConfigureDoor(exit, CurrentRoom.Exit, false);

            Vector2Int safeCell = CurrentRoom.Entry.Cell - DoorPlacement.Outward(CurrentRoom.Entry.Direction) * 3;
            if (!CurrentRoom.IsWalkable(safeCell.x, safeCell.y))
                throw new InvalidOperationException("A entrada não tem uma célula segura para o jogador.");
            Vector3 spawn = room.CellCenter(safeCell);
            // Existing Player collider is centered below the sprite's transform.
            var playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider != null) spawn -= (Vector3)playerCollider.offset;
            player.transform.position = spawn;
            playerBody.position = spawn;
            Physics2D.SyncTransforms();
            CurrentRoomIndex = CurrentRoomIndex == int.MaxValue ? int.MaxValue : CurrentRoomIndex + 1;
            RoomGenerated?.Invoke(CurrentRoom);

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, diceRoller.resultHold));
            playerLight.SetActive(true);
            roomLight.SetActive(true);
            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayGroup.alpha = 1f - Mathf.Clamp01(elapsed / revealDuration);
                yield return null;
            }
            darknessOverlay.SetActive(false);
            yield return exit.Open();
            playerBody.simulated = true;
            Physics2D.SyncTransforms();
            player.enabled = true;
            IsTransitioning = false;
            RoomRevealed?.Invoke(CurrentRoom);
        }

        private void ConfigureDoor(CatacombDoor door, DoorPlacement placement, bool isEntry)
        {
            door.gameObject.SetActive(true);
            door.transform.position = room.DoorCenter(placement);
            door.Configure(placement, isEntry);
        }

        private void OnDestroy()
        {
            if (outsideDoor != null) outsideDoor.Traversed -= OnDoorTraversed;
            if (exit != null) exit.Traversed -= OnDoorTraversed;
        }
    }
}
