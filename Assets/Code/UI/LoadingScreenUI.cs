using System.Collections;
using Code.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Localization;

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

        // titleKey и messageKey — именно ключи в таблице локализации!
        public void Show(string titleKey, string messageKey, int percentage = 100)
        {
            loadingScreenUI.SetActive(true);
            LocalizationHelper.SetLocalizedTextAsync(titleText, titleKey);
            LocalizationHelper.SetLocalizedTextAsync(messageText, messageKey);

            percentageSlider.value = percentage;
            percentageText.text = percentage.ToString("F0") + "%";
            percentageSlider.gameObject.SetActive(percentage > 1 && percentage < 100);
        }

        public void LoadScene(string sceneName, string titleKey = "loading.please_wait", string messageKey = "loading")
        {
            StartCoroutine(LoadRoutine(sceneName, titleKey, messageKey));
        }

        IEnumerator LoadRoutine(string sceneName, string titleKey, string messageKey)
        {
            loadingScreenUI.SetActive(true);
            yield return null;
            PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);

            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOperation != null)
            {
                asyncOperation.allowSceneActivation = false;
                while (!asyncOperation.isDone)
                {
                    percentageSlider.value = asyncOperation.progress * 100;
                    percentageText.text = (asyncOperation.progress * 100).ToString("F0") + "%";
                    LocalizationHelper.SetLocalizedTextAsync(titleText, titleKey);
                    LocalizationHelper.SetLocalizedTextAsync(messageText, messageKey);

                    if (asyncOperation.progress >= 0.9f)
                    {
                        percentageSlider.value = 100;
                        percentageText.text = "100%";
                        LocalizationHelper.SetLocalizedTextAsync(messageText, "loading.start_scene"); // Ключ для "Starting scene..."
                        if (!asyncOperation.allowSceneActivation)
                        {
                            asyncOperation.allowSceneActivation = true;
                        }
                    }
                    yield return null;
                }
            }
        }
    }
}
