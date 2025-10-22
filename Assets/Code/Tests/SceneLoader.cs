using Code.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Tests
{
    public class SceneLoader : MonoBehaviour
    {
        public string sceneName = "Main";
        public KeyCode key = KeyCode.None;
        public bool isDisconnect;
        
        private void Update()
        {
            if (key == KeyCode.None) return;
            if (Input.GetKeyDown(key))
                Load(sceneName);
        }

        public async void Load(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return;
            LoadingScreenUI.Instance.LoadScene(nameOrPath);
        }

        public void LoadPreviousScene()
        {
            LoadingScreenUI.Instance.LoadScene(PlayerPrefs.GetString("PreviousScene"));
        }

        public void Load(int buildIndex)
        {
            LoadingScreenUI.Instance.LoadScene(SceneManager.GetSceneByBuildIndex(buildIndex).name);
            if (isDisconnect)
                Code.Network.Lobby.LobbyDisconnector.Disconnect();
        }
    }
}