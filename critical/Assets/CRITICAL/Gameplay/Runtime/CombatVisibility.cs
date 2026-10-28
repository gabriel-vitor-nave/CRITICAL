using UnityEngine;

namespace Critical.Gameplay
{
    public static class CombatVisibility
    {
        private static readonly RaycastHit2D[] Hits = new RaycastHit2D[64];

        // Reused on Unity's main thread. Characters and triggers do not hide solid terrain.
        public static bool IsClear(Vector2 start, Vector2 end)
        {
            int count = Physics2D.LinecastNonAlloc(start, end, Hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = Hits[i].collider;
                if (collider == null || collider.isTrigger) continue;
                if (collider.GetComponentInParent<PlayerMovement>() != null ||
                    collider.GetComponentInParent<DemoEnemy>() != null) continue;
                return false;
            }
            // A saturated buffer may have omitted terrain behind the reported hits.
            return count < Hits.Length;
        }

        public static bool HasClearance(Vector2 start, Vector2 end, float radius)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude < 0.0001f) return true;
            Vector2 side = new Vector2(-direction.y, direction.x).normalized * radius;
            return IsClear(start, end) && IsClear(start + side, end + side) && IsClear(start - side, end - side);
        }
    }
}
