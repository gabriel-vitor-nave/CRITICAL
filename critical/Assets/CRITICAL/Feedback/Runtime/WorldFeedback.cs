using TMPro;
using UnityEngine;

namespace Critical.Feedback
{
    // Geometric combat VFX are placeholders; the team's sprites stay untouched.
    public sealed class WorldFeedback : MonoBehaviour
    {
        private static Sprite square;
        private float age, duration;
        private SpriteRenderer[] particles;
        private Vector3[] velocities;
        private Color color;
        private TextMeshPro label;
        private LineRenderer slash;

        public static void Number(Vector3 position, string text, Color tint)
        {
            if (!Application.isPlaying) return;
            var root = new GameObject("FloatingFeedback");
            root.transform.position = position;
            var effect = root.AddComponent<WorldFeedback>();
            effect.duration = 0.8f;
            effect.color = tint;
            effect.label = root.AddComponent<TextMeshPro>();
            effect.label.text = text;
            effect.label.fontSize = 4f;
            effect.label.fontStyle = FontStyles.Bold;
            effect.label.alignment = TextAlignmentOptions.Center;
            effect.label.rectTransform.sizeDelta = new Vector2(5f, 1f);
            effect.label.color = tint;
            effect.label.GetComponent<MeshRenderer>().sortingOrder = 120;
        }

        public static void Burst(Vector3 position, Color tint, int count = 8)
        {
            if (!Application.isPlaying) return;
            if (square == null)
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f, Texture2D.whiteTexture.width);
            var root = new GameObject("PLACEHOLDER_ImpactSparks");
            root.transform.position = position;
            var effect = root.AddComponent<WorldFeedback>();
            effect.duration = 0.35f;
            effect.color = tint;
            count = Mathf.Clamp(count, 1, 24);
            effect.particles = new SpriteRenderer[count];
            effect.velocities = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var piece = new GameObject("Spark");
                piece.transform.SetParent(root.transform, false);
                piece.transform.localScale = Vector3.one * Random.Range(0.04f, 0.11f);
                var renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = square;
                renderer.sortingOrder = 110;
                renderer.color = tint;
                effect.particles[i] = renderer;
                effect.velocities[i] = (Vector3)Random.insideUnitCircle.normalized * Random.Range(1.2f, 4f);
            }
        }

        public static void Slash(Vector2 position, Vector2 direction, float radius)
        {
            if (!Application.isPlaying) return;
            var root = new GameObject("PLACEHOLDER_MeleeArc");
            root.transform.position = position;
            var effect = root.AddComponent<WorldFeedback>();
            effect.duration = 0.16f;
            effect.color = new Color(1f, 0.87f, 0.55f);
            effect.slash = root.AddComponent<LineRenderer>();
            effect.slash.useWorldSpace = false;
            effect.slash.positionCount = 13;
            effect.slash.startWidth = 0.12f;
            effect.slash.endWidth = 0.035f;
            effect.slash.sortingOrder = 105;
            effect.slash.sharedMaterial = SpriteMaterial;
            float angle = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < 13; i++)
            {
                float a = angle + Mathf.Lerp(-1.2f, 1.2f, i / 12f);
                effect.slash.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.8f);
            }
        }

        private static Material spriteMaterial;
        private static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null) spriteMaterial = new Material(Shader.Find("Sprites/Default"));
                return spriteMaterial;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            Color faded = color;
            faded.a *= 1f - t;
            if (label != null)
            {
                transform.position += Vector3.up * Time.deltaTime;
                label.color = faded;
                transform.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.18f);
            }
            if (slash != null)
            {
                slash.startColor = slash.endColor = faded;
                transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f, t);
            }
            if (particles != null)
                for (int i = 0; i < particles.Length; i++)
                {
                    particles[i].transform.localPosition += velocities[i] * Time.deltaTime;
                    particles[i].color = faded;
                    particles[i].transform.localScale *= Mathf.Exp(-5f * Time.deltaTime);
                }
            if (age >= duration) Destroy(gameObject);
        }
    }
}
