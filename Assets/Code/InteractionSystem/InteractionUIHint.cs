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
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            HidePrompt();
        }

        public void ShowPrompt(string message)
        {
            _promptUI.SetActive(true);
            _promptText.text = message;
        }

        public void HidePrompt()
        {
            _promptUI.SetActive(false);
        }
    }
}