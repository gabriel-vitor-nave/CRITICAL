using UnityEngine;
using UnityEngine.EventSystems;

namespace Critical.UI
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class JuicyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private UnityEngine.UI.Button button;
        private Vector3 restScale;
        private bool hovered, selected, pressed;
        public System.Action Selected;

        private void Awake()
        {
            button = GetComponent<UnityEngine.UI.Button>();
            restScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<UnityEngine.UI.Button>();
            if (restScale.sqrMagnitude < 0.001f) restScale = Vector3.one;
            hovered = selected = pressed = false;
        }

        private void Update()
        {
            if (button == null) return;
            float scale = !button.interactable ? 1f : pressed ? 0.96f : hovered || selected ? 1.035f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, restScale * scale,
                1f - Mathf.Exp(-24f * Time.unscaledDeltaTime));
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }
        public void OnPointerDown(PointerEventData data) { pressed = true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }
        public void OnSelect(BaseEventData data) { selected = true; Selected?.Invoke(); }
        public void OnDeselect(BaseEventData data) { selected = pressed = false; }
    }
}
