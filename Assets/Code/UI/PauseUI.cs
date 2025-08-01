using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class PauseUI : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;

        private bool isPaused;
        
        private void OnEnable()
        {
            continueButton.onClick.AddListener(OnContinueClick);
            settingsButton.onClick.AddListener(OnSettingsClick);
            quitButton.onClick.AddListener(OnQuitClick);
        }

        private void OnDisable()
        {
            continueButton.onClick.RemoveListener(OnContinueClick);
            settingsButton.onClick.RemoveListener(OnSettingsClick);
            quitButton.onClick.RemoveListener(OnQuitClick);
        }

        private void OnPauseClick()
        {
            if (!pausePanel.activeSelf)
                pausePanel.SetActive(true);
            if (settingsPanel.activeSelf)
                settingsPanel.SetActive(false);

            isPaused = true;
        }
        
        private void OnContinueClick()
        {
            if (pausePanel.activeSelf)
                pausePanel.SetActive(false);
            if (settingsPanel.activeSelf)
                settingsPanel.SetActive(false);

            isPaused = false;
            CursorManager.Instance.HideCursor();
        }
        
        private void OnSettingsClick()
        {
            if (pausePanel.activeSelf)
                pausePanel.SetActive(false);
            if (!settingsPanel.activeSelf)
                settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (!pausePanel.activeSelf)
                pausePanel.SetActive(true);
            if (settingsPanel.activeSelf)
                settingsPanel.SetActive(false);
        }

        private void Update()
        {
            if (isPaused)
                if (CursorManager.Instance != null)
                    CursorManager.Instance.ShowCursor();
            
            if (settingsPanel.activeSelf)
                return;
            
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                isPaused = !isPaused;
            }
            
            if (pausePanel.activeSelf != isPaused)
            {
                if (isPaused)
                    OnPauseClick();
                else
                    OnContinueClick();
            }
        }

        private void OnQuitClick()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}