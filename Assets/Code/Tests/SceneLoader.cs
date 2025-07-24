using System;
using Code.Network.Lobby;
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
            if (key != KeyCode.None)
            {
                if (Input.GetKeyDown(key))
                {
                    Load (sceneName);
                }
            }
        }

        public void Load(string sceneName)
        {
            SceneManager.LoadSceneAsync(sceneName);
            if (isDisconnect)
                LobbyAutoDisconnect.Disconnect();
        }
        
        public void Load(int id)
        {
            SceneManager.LoadScene(id);
        }
    }
}
