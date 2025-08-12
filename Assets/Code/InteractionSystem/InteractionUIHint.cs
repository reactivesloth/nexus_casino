using TMPro;
using UnityEngine;

namespace Code.InteractionSystem
{
    public class InteractionUIHint : MonoBehaviour
    {
        public static InteractionUIHint Instance { get; private set; }

        [SerializeField] private GameObject _promptUI;
        [SerializeField] private TextMeshProUGUI _promptText;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            HidePrompt();
        }

        public void ShowPrompt(string message)
        {
            if (_promptUI != null) _promptUI.SetActive(true);
            if (_promptText != null) _promptText.text = message ?? string.Empty;
        }

        public void HidePrompt()
        {
            if (_promptUI != null) _promptUI.SetActive(false);
        }
    }
}