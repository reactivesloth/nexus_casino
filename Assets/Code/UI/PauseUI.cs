using Code.Tests;
using Code.UI.Admin;
using Code.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class PauseUI : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button adminButton;
        [SerializeField] private Button boutiqueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;

        private bool _isPaused;
        
        public static PauseUI Instance { get; private set; }
        public bool IsPaused => _isPaused;
        
        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClick);
            if (adminButton != null) adminButton.onClick.AddListener(OnAdminButtonClick);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClick);
            if (boutiqueButton != null) boutiqueButton.onClick.AddListener(OnBoutiqueClick);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClick);
        }

        private void OnDisable()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueClick);
            if (adminButton != null) adminButton.onClick.RemoveListener(OnAdminButtonClick);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClick);
            if (boutiqueButton != null) settingsButton.onClick.RemoveListener(OnBoutiqueClick);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClick);
        }

        private void Update()
        {
            // поддерживаем видимость курсора только когда на паузе
            if (_isPaused && CursorManager.Instance != null)
                CursorManager.Instance.ShowCursor();

            if (PlayerInput.Instance.IsPausedDown)
            {
                if (!_isPaused) 
                    OnPauseClick();
                else 
                    OnContinueClick();
            }
        }

        private void OnAdminButtonClick()
        {
            AdminControlPanel panel = FindAnyObjectByType<AdminControlPanel>();
            panel.SetActive(true);
        }
        
        private void OnBoutiqueClick()
        {
            LoadingScreenUI.Instance.LoadScene("Character Customization");
        }
        
        private void OnPauseClick()
        {
            if (pausePanel != null && !pausePanel.activeSelf) pausePanel.SetActive(true);
            if (settingsPanel != null && settingsPanel.activeSelf) settingsPanel.SetActive(false);
            _isPaused = true;
        }

        private void OnContinueClick()
        {
            if (pausePanel != null && pausePanel.activeSelf) pausePanel.SetActive(false);
            if (settingsPanel != null && settingsPanel.activeSelf) settingsPanel.SetActive(false);

            _isPaused = false;
            if (CursorManager.Instance != null) CursorManager.Instance.HideCursor();
        }

        private void OnSettingsClick()
        {
            if (pausePanel != null && pausePanel.activeSelf) pausePanel.SetActive(false);
            if (settingsPanel != null && !settingsPanel.activeSelf) settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (pausePanel != null && !pausePanel.activeSelf) pausePanel.SetActive(true);
            if (settingsPanel != null && settingsPanel.activeSelf) settingsPanel.SetActive(false);
        }

        private void OnQuitClick()
        {
            LoadingScreenUI.Instance.LoadScene("Init");
// #if UNITY_EDITOR
//             UnityEditor.EditorApplication.isPlaying = false;
// #else
//             Application.Quit();
// #endif
        }
    }
}
