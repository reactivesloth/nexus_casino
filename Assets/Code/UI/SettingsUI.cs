using System.Collections.Generic;
using Code.Utility;
using Ricimi;
using UnityEngine;
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

        [Header("Graphics")]
        public TextSelectionSlider qualityDropdown;

        [Header("Camera & Controls")]
        public Slider cameraSensitivitySlider;
        public Toggle invertCameraToggleOn;   // true  = invert on
        public Toggle invertCameraToggleOff;  // false = invert off

        [Header("Buttons")]
        public Button applyButton;
        public Button cancelButton;
        public Button resetDefaultsButton;

        private struct SettingsSnapshot
        {
            public float Voice, Music, Slots, Sfx;   // 0..100
            public int QualityLevel;
            public float CameraSensitivity;
            public bool InvertCamera;

            public void LoadFromManager(SettingsManager sm)
            {
                Voice            = sm.VoiceChatVolume;
                Music            = sm.MusicVolume;
                Slots            = sm.SlotsVolume;
                Sfx              = sm.SFXVolume;
                QualityLevel     = sm.QualityLevel;
                CameraSensitivity= sm.CameraSensitivity;
                InvertCamera     = sm.InvertCamera;
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
                CameraSensitivity = 40f;
                InvertCamera      = false;
            }

            public void ApplyToManager(SettingsManager sm)
            {
                sm.SetVoiceVolume(Voice);
                sm.SetMusicVolume(Music);
                sm.SetSlotsVolume(Slots);
                sm.SetSFXVolume(Sfx);

                sm.SetGraphicsQuality(QualityLevel);
                sm.SetCameraSensitivity(CameraSensitivity);
                sm.SetInvertCamera(InvertCamera);
            }
        }

        private SettingsSnapshot _draft;   // то, что редактирует пользователь в UI
        private bool _suppressUiEvents;    // чтобы не ловить колбеки, когда программно выставляем значения

        private void Start()
        {
            if (SettingsManager.Instance == null)
            {
                Debug.LogError("SettingsManager.Instance == null. SettingsUI init aborted.");
                enabled = false;
                return;
            }

            PopulateQualityDropdown();
            HookUiEvents();

            // загрузить текущие активные настройки в черновик и в UI
            _draft.LoadFromManager(SettingsManager.Instance);
            PushDraftToUI();

            if (applyButton != null)        applyButton.onClick.AddListener(ApplySettings);
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
        }

        private void OnSettingsApplied()
        {
            // Если в другом месте применили — подтянем актуальные значения в наш черновик и UI
            _draft.LoadFromManager(SettingsManager.Instance);
            PushDraftToUI();
        }

        private void PopulateQualityDropdown()
        {
            if (qualityDropdown == null) return;
            qualityDropdown.ClearOptions();
            var names = new List<string>(QualitySettings.names);
            qualityDropdown.AddOptions(names);
        }

        private void HookUiEvents()
        {
            if (voiceChatSlider != null) voiceChatSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Voice = v; });
            if (musicSlider != null)     musicSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Music = v; });
            if (slotsSlider != null)     slotsSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Slots = v; });
            if (sfxSlider != null)       sfxSlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.Sfx = v; });

            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(i => { if (!_suppressUiEvents) _draft.QualityLevel = i; });

            if (cameraSensitivitySlider != null) cameraSensitivitySlider.onValueChanged.AddListener(v => { if (!_suppressUiEvents) _draft.CameraSensitivity = v; });

            if (invertCameraToggleOn != null)  invertCameraToggleOn.onValueChanged.AddListener(v =>
            {
                if (_suppressUiEvents) return;
                if (v)
                {
                    _draft.InvertCamera = true;
                    if (invertCameraToggleOff != null) { _suppressUiEvents = true; invertCameraToggleOff.isOn = false; _suppressUiEvents = false; }
                }
            });
            if (invertCameraToggleOff != null) invertCameraToggleOff.onValueChanged.AddListener(v =>
            {
                if (_suppressUiEvents) return;
                if (v)
                {
                    _draft.InvertCamera = false;
                    if (invertCameraToggleOn != null) { _suppressUiEvents = true; invertCameraToggleOn.isOn = false; _suppressUiEvents = false; }
                }
            });
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

            if (cameraSensitivitySlider != null) cameraSensitivitySlider.value = _draft.CameraSensitivity;

            if (invertCameraToggleOn != null)  invertCameraToggleOn.isOn  = _draft.InvertCamera;
            if (invertCameraToggleOff != null) invertCameraToggleOff.isOn = !_draft.InvertCamera;

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
