using UnityEngine;

namespace Critical.UI
{
    /// <summary>Code-native outlines for the supplied HUD wireframe; no texture or font dependency.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiSketchGraphic : UnityEngine.UI.Graphic
    {
        public UiSketchGraphic() { useLegacyMeshGeneration = false; }

        protected override void OnEnable()
        {
            // Runtime HUD objects from an earlier script version may predate RequireComponent.
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        public enum OutlineShape { Rectangle, Circle, Diamond }
        [SerializeField] private OutlineShape shape;
        [SerializeField] private Color stroke = new Color(0.86f, 0.86f, 0.84f, 1f);
        [SerializeField] private Color fill = new Color(0.055f, 0.055f, 0.055f, 0.96f);
        [SerializeField] private float thickness = 2f;
        [SerializeField] private bool hatched;
        [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;

        public void Configure(OutlineShape outline, Color border, Color background, float width = 2f)
        {
            shape = outline; stroke = border; fill = background; thickness = Mathf.Max(0f, width);
            SetVerticesDirty();
        }

        public void SetHealth(float amount, bool empty)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(fillAmount - amount) < 0.002f && hatched == empty) return;
            fillAmount = amount; hatched = empty; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            const int count = 36;
            Color body = fill * color;
            if (shape == OutlineShape.Circle) body.a *= Mathf.Lerp(0.1f, 1f, fillAmount);
            Vector2 center = rect.center;
            if (body.a > 0f)
            {
                int first = vh.currentVertCount;
                vh.AddVert(center, body, Vector2.zero);
                for (int i = 0; i <= count; i++) vh.AddVert(Point(rect, i % count, count), body, Vector2.zero);
                for (int i = 0; i < count; i++) vh.AddTriangle(first, first + i + 1, first + i + 2);
            }
            Color line = stroke * color;
            if (thickness > 0f && line.a > 0f)
                for (int i = 0; i < count; i++)
                    Segment(vh, Point(rect, i, count), Point(rect, (i + 1) % count, count), thickness, line);
            if (hatched && shape == OutlineShape.Circle)
            {
                float radius = Mathf.Min(rect.width, rect.height) * 0.36f;
                Color hatch = line; hatch.a *= 0.8f;
                for (int i = -2; i <= 2; i++)
                {
                    float offset = i * radius * 0.33f;
                    float extent = Mathf.Sqrt(Mathf.Max(0f, radius * radius - offset * offset));
                    Vector2 along = new Vector2(0.7071f, 0.7071f) * extent;
                    Vector2 side = new Vector2(-0.7071f, 0.7071f) * offset;
                    Segment(vh, center + side - along, center + side + along, 1.2f, hatch);
                    Segment(vh, center - side + new Vector2(-along.x, along.y), center - side + new Vector2(along.x, -along.y), 1.2f, hatch);
                }
            }
        }

        private Vector2 Point(Rect rect, int index, int count)
        {
            Vector2 point;
            float inset = thickness * 0.55f + 1f;
            rect = new Rect(rect.x + inset, rect.y + inset, Mathf.Max(0f, rect.width - inset * 2f), Mathf.Max(0f, rect.height - inset * 2f));
            if (shape == OutlineShape.Circle || shape == OutlineShape.Diamond)
            {
                float angle = index * Mathf.PI * 2f / count;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float factor = shape == OutlineShape.Diamond ? 1f / (Mathf.Abs(direction.x) + Mathf.Abs(direction.y)) : 1f;
                point = rect.center + Vector2.Scale(direction, rect.size * 0.5f) * factor;
            }
            else
            {
                float radius = Mathf.Min(18f, Mathf.Min(rect.width, rect.height) * 0.15f);
                int quarter = index / 9;
                float angle = (quarter + (index % 9) / 8f) * Mathf.PI * 0.5f;
                Vector2 corner = new Vector2(quarter == 0 || quarter == 3 ? rect.xMax - radius : rect.xMin + radius,
                    quarter < 2 ? rect.yMax - radius : rect.yMin + radius);
                point = corner + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            // Stable irregularity follows the reference's pen line without per-frame random noise.
            float roughness = Mathf.Min(0.75f, Mathf.Min(rect.width, rect.height) * 0.012f);
            return point + new Vector2(Mathf.Sin(index * 2.73f), Mathf.Cos(index * 3.19f)) * roughness;
        }

        private static void Segment(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            int index = vh.currentVertCount;
            vh.AddVert(a - side, tint, Vector2.zero); vh.AddVert(a + side, tint, Vector2.zero);
            vh.AddVert(b + side, tint, Vector2.zero); vh.AddVert(b - side, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index + 2, index + 3, index);
        }
    }
}
