using UnityEngine;
using UnityEngine.EventSystems;

namespace Critical.UI
{
    public sealed class MenuGameActions : MonoBehaviour
    {
        [SerializeField] public MenuTransition menuTransition;
        [SerializeField] public UnityEngine.UI.Button startButton;

        private void Start()
        {
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (SceneTransition.IsLoading || menuTransition == null) return;
            bool optionsVisible = menuTransition.menuOptions != null && menuTransition.menuOptions.gameObject.activeInHierarchy;
            if (Input.GetKeyDown(KeyCode.Escape) && optionsVisible)
                menuTransition.FecharOpcoes();
            // A selected button already receives Submit from the EventSystem.
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                && !optionsVisible && (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null))
                StartRun();
        }

        public void StartRun()
        {
            if (menuTransition != null && ((menuTransition.menuOptions != null && menuTransition.menuOptions.gameObject.activeInHierarchy)
                || (menuTransition.menuPrincipal != null && !menuTransition.menuPrincipal.gameObject.activeInHierarchy))) return;
            SceneTransition.Load(SceneTransition.GameScene);
        }

        public void QuitGame()
        {
            if (SceneTransition.IsLoading) return;
#if UNITY_EDITOR
            Debug.Log("Sair: a aplicação será encerrada na build. O Editor permanece aberto.", this);
#else
            Application.Quit();
#endif
        }
    }
}
