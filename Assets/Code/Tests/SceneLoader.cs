using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Tests
{
    public class SceneLoader : MonoBehaviour
    {
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
