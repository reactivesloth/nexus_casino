using System;
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
            okButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            okButton.onClick.RemoveListener(Close);
        }

        public void Show(string title, string description = "")
        {
            titleText.text = title;
            descriptionText.text = description;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}