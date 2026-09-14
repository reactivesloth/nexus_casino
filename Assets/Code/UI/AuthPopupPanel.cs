using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class AuthPopupPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText, descriptionText;
        [SerializeField] private Button okButton;

        private void OnEnable()
        {
            if (okButton != null)
                okButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (okButton != null)
                okButton.onClick.RemoveListener(Close);
        }

        public void Show(string title, string description = "")
        {
            if (titleText != null) titleText.text = title ?? string.Empty;
            if (descriptionText != null) descriptionText.text = description ?? string.Empty;
            gameObject.SetActive(true);
        }

        public void Close() => gameObject.SetActive(false);
    }
}