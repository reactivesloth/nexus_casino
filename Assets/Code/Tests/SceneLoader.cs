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

            var op = SceneManager.LoadSceneAsync(nameOrPath);
            if (op != null)
            {
                op.allowSceneActivation = true;
                while (!op.isDone) await System.Threading.Tasks.Task.Yield();
            }

            if (isDisconnect)
                Code.Network.Lobby.LobbyAutoDisconnect.Disconnect();
        }

        public void Load(int buildIndex)
        {
            SceneManager.LoadScene(buildIndex);
            if (isDisconnect)
                Code.Network.Lobby.LobbyAutoDisconnect.Disconnect();
        }
    }
}