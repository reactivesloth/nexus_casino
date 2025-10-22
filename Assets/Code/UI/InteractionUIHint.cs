using Code.Utility;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    public class InteractionUIHint : MonoBehaviour
    {
        public static InteractionUIHint Instance { get; private set; }

        [SerializeField] private GameObject _promptUI;
        [SerializeField] private TextMeshProUGUI _promptText;

        private bool _initedHint;

        private void EnsureInit()
        {
            if (_initedHint) return;
            _initedHint = true;
            if (_promptUI != null && !_promptUI.activeSelf)
                _promptUI.SetActive(false);
            if (_promptText != null && string.IsNullOrEmpty(_promptText.text))
                _promptText.text = string.Empty;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInit();
            HidePrompt();
        }

        public void ShowPrompt(string message)
        {
            EnsureInit();
            if (_promptUI != null) _promptUI.SetActive(true);
            if (_promptText != null) 
                LocalizationHelper.SetLocalizedTextAsync(_promptText, message);
            if (string.IsNullOrEmpty(message))
                HidePrompt();
        }

        public void HidePrompt()
        {
            EnsureInit();
            if (_promptUI != null) _promptUI.SetActive(false);
        }
    }
}