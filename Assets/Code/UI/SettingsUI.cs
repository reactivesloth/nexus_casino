using System.Collections.Generic;
using Code.Utility;
using Ricimi;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// Связка UI ↔ SettingsManager с отложенным применением:
    /// - Изменения пишутся в локальный снапшот (_draft), не применяются сразу.
    /// - Apply() → сохраняет в PlayerPrefs и применяет через SettingsManager.
    /// - Cancel() → возвращает UI к текущим активным настройкам (что сейчас в SettingsManager).
    /// - ResetToDefaults() → заполняет UI дефолтами (не применяет, пока не нажмёшь Apply).
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [Header("Audio (0..100)")]
        public Slider voiceChatSlider;
        public Slider musicSlider;
        public Slider slotsSlider;
        public Slider sfxSlider;

        [Header("Game")]
        public TextSelectionSlider localizationDropdown;
        public TextSelectionSlider invertCameraDropdown;
        public TextSelectionSlider qualityDropdown;
        public TextSelectionSlider fpsDropdown;
        public Slider cameraSensitivitySlider;

        [Header("Buttons")]
        public Button applyButton;
        public Button closeButton;
        public Button cancelButton;
        public Button resetDefaultsButton;

        private struct SettingsSnapshot
        {
            public float Voice, Music, Slots, Sfx;   // 0..100
            public int QualityLevel;
            public int FPSLimit;
            public float CameraSensitivity;
            public bool InvertCamera;
            public Locale Localization;

            public void LoadFromManager(SettingsManager sm)
            {
                Voice            = sm.VoiceChatVolume;
                Music            = sm.MusicVolume;
                Slots            = sm.SlotsVolume;
                Sfx              = sm.SFXVolume;
                QualityLevel     = sm.QualityLevel;
                FPSLimit         = sm.FPSLimit;
                CameraSensitivity= sm.CameraSensitivity;
                InvertCamera     = sm.InvertCamera;
                Localization     = sm.Localization;
            }

            public void LoadDefaults()
            {
                // Берём дефолты из менеджера через reset в теневом режиме:
                // трюк: создаём временный снапшот и заполняем его, не трогая живые настройки
                var sm = SettingsManager.Instance;
                if (sm == null) return;

                // Значения из ResetToDefaults: чтобы не модифицировать глобальные настройки,
                // читаем из PlayerPrefs после временного сброса в локальные переменные — но это шумно.
                // Проще — зафиксируем те же дефолты, что и в SettingsManager:
                Voice             = 50f;  // см. Defaults в SettingsManager
                Music             = 30f;
                Slots             = 30f;
                Sfx               = 30f;
                QualityLevel      = QualitySettings.GetQualityLevel();
                FPSLimit          = 30;
                CameraSensitivity = 40f;
                InvertCamera      = false;
                Localization      = LocalizationSettings.ProjectLocale;
            }

            public void ApplyToManager(SettingsManager sm)
            {
                sm.SetVoiceVolume(Voice);
                sm.SetMusicVolume(Music);
                sm.SetSlotsVolume(Slots);
                sm.SetSFXVolume(Sfx);
                sm.SetFPSLimit(FPSLimit);
                sm.SetGraphicsQuality(QualityLevel);
                sm.SetCameraSensitivity(CameraSensitivity);
                sm.SetInvertCamera(InvertCamera);
                sm.SetLanguage(Localization);
            }
        }

        private SettingsSnapshot _draft;   // то, что редактирует пользователь в UI
        private bool _suppressUiEvents;    // чтобы не ловить колбеки, когда программно выставляем значения

        private async void Start()
        {
            await LocalizationSettings.InitializationOperation.Task;
            
            if (SettingsManager.Instance == null)
            {
                Debug.LogError("SettingsManager.Instance == null. SettingsUI init aborted.");
                enabled = false;
                return;
            }

            PopulateQualityDropdown();
            PopulateFPSDropdown();
            PopulateLocalizationDropdown();
            PopulateInvertCameraYDropdown();
            HookUiEvents();

            // загрузить текущие активные настройки в черновик и в UI
            _draft.LoadFromManager(SettingsManager.Instance);
            PushDraftToUI();

            if (applyButton != null)        applyButton.onClick.AddListener(ApplySettings);
            if (closeButton != null)        closeButton.onClick.AddListener(ApplySettings);
            if (cancelButton != null)       cancelButton.onClick.AddListener(CancelChanges);
            if (resetDefaultsButton != null)resetDefaultsButton.onClick.AddListener(ResetToDefaultsDraft);
        }

        private void OnEnable()
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.OnSettingsApplied += OnSettingsApplied;
        }
        private void OnDisable()
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.OnSettingsApplied -= OnSettingsApplied;
            
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnSettingsApplied()
        {
            // Если в другом месте применили — подтянем актуальные значения в наш черновик и UI
            _draft.LoadFromManager(SettingsManager.Instance);
            PushDraftToUI();
        }

        private void PopulateInvertCameraYDropdown()
        {
            if (invertCameraDropdown == null) return;
            invertCameraDropdown.ClearOptions();
            var names = new List<string>{"on", "off"};
            invertCameraDropdown.GetComponent<TextSelectionSliderLocalizationHelper>().InitKeys(names);
        }

        private void PopulateFPSDropdown()
        {
            if (fpsDropdown == null) return;
            fpsDropdown.ClearOptions();
            var names = new List<string>{"15", "30", "60", "Unlimited"};
            fpsDropdown.GetComponent<TextSelectionSliderLocalizationHelper>().InitKeys(names);
        }
        
        private void PopulateQualityDropdown()
        {
            if (qualityDropdown == null) return;
            qualityDropdown.ClearOptions();
            var names = new List<string>(QualitySettings.names);
            qualityDropdown.GetComponent<TextSelectionSliderLocalizationHelper>().InitKeys(names);
        }

        private void PopulateLocalizationDropdown()
        {
            localizationDropdown.ClearOptions();

            var locales = LocalizationSettings.AvailableLocales.Locales;
            var localeNames = new List<string>();

            foreach (var locale in locales)
            {
                localeNames.Add(locale.LocaleName);
            }

            if (localizationDropdown == null) return;
            localizationDropdown.ClearOptions();
            localizationDropdown.AddOptions(localeNames);
            
            OnLocaleChanged(SettingsManager.Instance.Localization);
            
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale locale)
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            int index = locales.IndexOf(locale);
            
            if (index >= 0)
            {
                localizationDropdown.value = index;
                PopulateQualityDropdown();
                PopulateFPSDropdown();
                PopulateInvertCameraYDropdown();
            }
        }
        
        private void HookUiEvents()
        {
            if (voiceChatSlider != null) voiceChatSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Voice = v; });
            if (musicSlider != null)     musicSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Music = v; });
            if (slotsSlider != null)     slotsSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Slots = v; });
            if (sfxSlider != null)       sfxSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Sfx = v; });
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.QualityLevel = v; });
            if (fpsDropdown != null) fpsDropdown.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.FPSLimit = v; });
            if (localizationDropdown != null) localizationDropdown.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Localization = LocalizationSettings.AvailableLocales.Locales[v]; LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[v];});
            if (cameraSensitivitySlider != null) cameraSensitivitySlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.CameraSensitivity = v; });
            if (invertCameraDropdown != null) invertCameraDropdown.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.InvertCamera = v != 0; });
        }

        private void PushDraftToUI()
        {
            _suppressUiEvents = true;

            if (voiceChatSlider != null) voiceChatSlider.value = _draft.Voice;
            if (musicSlider != null)     musicSlider.value     = _draft.Music;
            if (slotsSlider != null)     slotsSlider.value     = _draft.Slots;
            if (sfxSlider != null)       sfxSlider.value       = _draft.Sfx;

            if (qualityDropdown != null)
            {
                int max = qualityDropdown.Options.Count > 0 ? qualityDropdown.Options.Count - 1 : 0;
                qualityDropdown.value = Mathf.Clamp(_draft.QualityLevel, 0, max);
                qualityDropdown.RefreshShownValue();
            }

            if (fpsDropdown != null)
            {
                int max = fpsDropdown.Options.Count > 0 ? fpsDropdown.Options.Count - 1 : 0;
                fpsDropdown.value = Mathf.Clamp(_draft.FPSLimit, 0, max);
                fpsDropdown.RefreshShownValue();
            }
            
            if (invertCameraDropdown != null)
            {
                invertCameraDropdown.value = _draft.InvertCamera ? 1 : 0;
                invertCameraDropdown.RefreshShownValue();
            }

            if (cameraSensitivitySlider != null) cameraSensitivitySlider.value = _draft.CameraSensitivity;

            if (localizationDropdown != null)
            {
                OnLocaleChanged(_draft.Localization);
                localizationDropdown.RefreshShownValue();
            }
            
            _suppressUiEvents = false;
        }

        // === Кнопки ===

        private void ApplySettings()
        {
            var sm = SettingsManager.Instance;
            if (sm == null) return;

            _draft.ApplyToManager(sm);
            sm.SaveAllSettings();
            sm.ApplyAllSettings(); // единая точка применения (AudioManager/Quality/PostFX и т.д.)
        }

        private void CancelChanges()
        {
            var sm = SettingsManager.Instance;
            if (sm == null) return;

            _draft.LoadFromManager(sm);
            PushDraftToUI(); // просто вернули UI к текущим активным настройкам
        }

        private void ResetToDefaultsDraft()
        {
            _draft.LoadDefaults(); // заполняем «черновик» дефолтами (не трогаем систему)
            PushDraftToUI();
        }
    }
}
