using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.UI
{
    public class LoadingScreenUI : MonoBehaviour
    {
        public static LoadingScreenUI Instance;
        
        [SerializeField] private GameObject loadingScreenUI;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI percentageText;
        [SerializeField] private Slider percentageSlider;
        
        private void Awake()
        {
            if (Instance == null || Instance != this)
                Instance = this;
        }
        
        public void Hide()
        {
            loadingScreenUI.SetActive(false);
        }
        
        public void Show(string title, string message, int percentage = 100)
        {
            loadingScreenUI.SetActive(true);
            titleText.text = title;
            messageText.text = message;
            percentageSlider.value = percentage;
            percentageText.text = percentage.ToString("F0") + "%";;
        }
        
        public void LoadScene (string sceneName, string title = "Please wait...", string message = "Loading...")
        {
            StartCoroutine(LoadRoutine(sceneName, title, message));
        }

        IEnumerator LoadRoutine(string sceneName, string title, string message)
        {
            loadingScreenUI.SetActive(true);
            yield return null;

            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOperation != null)
            {
                asyncOperation.allowSceneActivation = false;
                Debug.Log("Pro :" + asyncOperation.progress);
                while (!asyncOperation.isDone)
                {
                    percentageSlider.value = asyncOperation.progress * 100;
                    percentageText.text = asyncOperation.progress * 100 + "%";
                    titleText.text = title;
                    messageText.text = message;
                    if (asyncOperation.progress >= 0.9f)
                    {
                        messageText.text = "Press any key to continue";
                        if (Input.anyKeyDown)
                        {
                            //loadingScreenUI.SetActive(false);
                            asyncOperation.allowSceneActivation = true;
                        }
                    }

                    yield return null;
                }
            }
        }
    }
}