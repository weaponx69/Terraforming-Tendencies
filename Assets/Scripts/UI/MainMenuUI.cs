using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        public Button startButton;
        public Button loadButton;
        public Button quitButton;

        [Header("Scene Settings")]
        public string gameplaySceneName = "Terraforming-Tendencies";

        private void Start()
        {
            // Fallback for automated setup
            if (startButton == null)
            {
                var go = GameObject.Find("Start Button");
                if (go != null) startButton = go.GetComponent<Button>();
            }

            if (loadButton == null)
            {
                var go = GameObject.Find("Load Button");
                if (go != null) loadButton = go.GetComponent<Button>();
            }

            if (quitButton == null)
            {
                var go = GameObject.Find("Quit Button");
                if (go != null) quitButton = go.GetComponent<Button>();
            }

            if (startButton != null)
                startButton.onClick.AddListener(StartGame);

            if (loadButton != null)
                loadButton.onClick.AddListener(LoadGame);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
        }

        public void StartGame()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void LoadGame()
        {
            // Set a flag to load the game once the scene is loaded
            PlayerPrefs.SetInt("LoadGameRequest", 1);
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
