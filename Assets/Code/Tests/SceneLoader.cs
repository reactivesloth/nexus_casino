using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Tests
{
    public class SceneLoader : MonoBehaviour
    {
        public string sceneName = "Main";
        public KeyCode key = KeyCode.None;

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
        }
        
        public void Load(int id)
        {
            SceneManager.LoadScene(id);
        }
    }
}
