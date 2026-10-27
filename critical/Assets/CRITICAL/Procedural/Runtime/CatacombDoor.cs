using System;
using System.Collections;
using UnityEngine;

namespace Critical.Procedural
{
    public sealed class CatacombDoor : MonoBehaviour
    {
        public SpriteRenderer leftHalf, rightHalf;
        public Sprite[] leftFrames, rightFrames;
        public Collider2D blocker;
        public BoxCollider2D passageTrigger;
        [Min(0f)] public float frameDuration = 0.08f;

        public event Action<CatacombDoor> Traversed;
        public bool IsOpen { get; private set; }
        public DoorDirection Direction { get; private set; }
        public bool IsEntry { get; private set; }
        public bool CanTraverse => IsOpen && !IsEntry && !opening && !traversed && isActiveAndEnabled
            && passageTrigger != null && passageTrigger.enabled && passageTrigger.gameObject.activeInHierarchy;

        private bool opening, traversed;
        private int configuration;

        public void Configure(DoorPlacement placement, bool entry)
        {
            if ((int)placement.Direction < 0 || (int)placement.Direction > 3)
                throw new ArgumentOutOfRangeException(nameof(placement));
            ValidateReferences();
            configuration++;
            Direction = placement.Direction;
            IsEntry = entry;
            IsOpen = opening = traversed = false;
            float angle = Direction == DoorDirection.North ? 90f : Direction == DoorDirection.West ? 180f
                : Direction == DoorDirection.South ? 270f : 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            blocker.isTrigger = false;
            blocker.enabled = true;
            if (blocker is BoxCollider2D box)
            {
                box.size = new Vector2(2f, 1f);
                box.offset = Vector2.zero;
            }
            passageTrigger.isTrigger = true;
            passageTrigger.size = new Vector2(2f, 1f);
            passageTrigger.offset = Vector2.zero;
            passageTrigger.enabled = false;
            SetFrame(0);
        }

        public IEnumerator Open()
        {
            if (IsOpen || opening) yield break;
            ValidateReferences();
            int version = configuration;
            opening = true;
            for (int frame = 0; frame < 5; frame++)
            {
                if (version != configuration || !isActiveAndEnabled) yield break;
                SetFrame(frame);
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, frameDuration));
            }
            if (version != configuration || !isActiveAndEnabled) yield break;
            opening = false;
            IsOpen = true;
            blocker.enabled = IsEntry;
            passageTrigger.enabled = !IsEntry;
            Physics2D.SyncTransforms();
        }

        public bool TryTraverse(Collider2D other)
        {
            if (!CanTraverse || other == null || !other.enabled || !other.gameObject.activeInHierarchy) return false;
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player == null || !player.isActiveAndEnabled || !passageTrigger.Distance(other).isOverlapped) return false;
            traversed = true;
            passageTrigger.enabled = false;
            Traversed?.Invoke(this);
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryTraverse(other);
        private void OnTriggerStay2D(Collider2D other) => TryTraverse(other);

        private void SetFrame(int frame)
        {
            leftHalf.sprite = leftFrames[frame];
            rightHalf.sprite = rightFrames[frame];
        }

        private void ValidateReferences()
        {
            if (leftHalf == null || rightHalf == null || blocker == null || passageTrigger == null
                || leftFrames == null || rightFrames == null || leftFrames.Length != 5 || rightFrames.Length != 5)
                throw new InvalidOperationException("CatacombDoor requires two renderers, five frame pairs, a blocker and a passage trigger.");
            for (int i = 0; i < 5; i++)
                if (leftFrames[i] == null || rightFrames[i] == null)
                    throw new InvalidOperationException("CatacombDoor has a missing animation frame.");
        }
    }
}
