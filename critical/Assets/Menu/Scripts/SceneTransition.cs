using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Critical.UI
{
    /// <summary>One transition owner for menu, retry, and return-to-menu buttons.</summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        public const string MenuScene = "Menu";
        public const string GameScene = "Exploration";
        public static bool IsLoading { get; private set; }

        private static SceneTransition instance;
        private CanvasGroup curtain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            IsLoading = false;
        }

        public static void Load(string sceneName)
        {
            if (IsLoading) return;
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Cena indisponível na build: " + sceneName);
                return;
            }

            if (instance == null)
            {
                var owner = new GameObject("SceneTransition");
                instance = owner.AddComponent<SceneTransition>();
                DontDestroyOnLoad(owner);
            }
            IsLoading = true;
            instance.StartCoroutine(instance.ChangeScene(sceneName));
        }

        private void Awake()
        {
            var canvasObject = new GameObject("TransitionCurtain", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            curtain = canvasObject.GetComponent<CanvasGroup>();
            curtain.alpha = 0f;
            curtain.blocksRaycasts = false;

            var background = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(canvasObject.transform, false);
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<UnityEngine.UI.Image>().color = new Color(0.025f, 0.02f, 0.035f, 1f);

            var label = new GameObject("LoadingLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(canvasObject.transform, false);
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.12f);
            rect.sizeDelta = new Vector2(720f, 48f);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.text = "PREPARANDO A PRÓXIMA APOSTA…";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20f;
            text.characterSpacing = 5f;
            text.color = new Color(0.94f, 0.77f, 0.43f);
            text.raycastTarget = false;
        }

        private IEnumerator ChangeScene(string sceneName)
        {
            curtain.blocksRaycasts = true;
            yield return Fade(0f, 1f, 0.24f);
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (System.Exception error) { Debug.LogException(error); }
            if (operation == null)
            {
                yield return Fade(1f, 0f, 0.2f);
                curtain.blocksRaycasts = false;
                IsLoading = false;
                yield break;
            }
            while (!operation.isDone) yield return null;
            // Paused and death screens must never carry a stopped clock into a new run.
            Time.timeScale = 1f;
            yield return null;
            yield return Fade(1f, 0f, 0.32f);
            curtain.blocksRaycasts = false;
            IsLoading = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                curtain.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            curtain.alpha = to;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            IsLoading = false;
        }
    }
}
