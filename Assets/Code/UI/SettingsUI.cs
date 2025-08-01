using System;
using System.Collections.Generic;
using Code.Utility;
using Ricimi;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// Привязывает UI-элементы к SettingsManager: заполняет значения, слушает изменения и применяет / сохраняет / сбрасывает.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [Header("Audio")]
        public Slider voiceChatSlider;
        public Slider musicSlider;
        public Slider slotsSlider;
        public Slider sfxSlider;

        [Header("Graphics")]
        public TextSelectionSlider qualityDropdown;
        public TextSelectionSlider resolutionDropdown;
        // public InputField fpsInputField;
        // public Toggle effectsToggle;
        // public Dropdown antiAliasingDropdown;

        [Header("Camera & Controls")]
        public Slider cameraSensitivitySlider;

        public Toggle invertCameraToggleOn;
        public Toggle invertCameraToggleOff;

        // [Header("Localization")]
        // [Tooltip("Список языковых кодов, например: en, ru")]
        // public List<string> languageCodes;
        // public List<string> languageDisplayNames; // то, что показывается в дропдауне
        // public Dropdown languageDropdown;

        [Header("Buttons")]
        public Button applyButton;
        public Button cancelButton;
        public Button resetDefaultsButton;

        // Вспомогательные структуры
        private List<int> resolutionOriginalIndices = new List<int>(); // для маппинга фильтрованных в оригинальные

        // Антиалиасинг: отображаемые + реальные значения
        private readonly int[] aaLevels = new[] { 0, 2, 4, 8 };
        private readonly string[] aaDisplay = new[] { "Off", "2x", "4x", "8x" };

        
        private void Start()
        {
            if (SettingsManager.Instance == null)
            {
                Debug.LogError("SettingsManager.Instance == null. UI не может инициализироваться.");
                return;
            }

            PopulateQualityDropdown();
            PopulateResolutionDropdown();
            // PopulateAntiAliasingDropdown();
            // PopulateLanguageDropdown();

            AddListeners();
            LoadUIFromSettings();

            applyButton.onClick.AddListener(ApplySettings);
            cancelButton.onClick.AddListener(RevertUI);
            resetDefaultsButton.onClick.AddListener(ResetToDefaults);
        }

        private void OnEnable()
        {
            SettingsManager.Instance.OnSettingsApplied += LoadUIFromSettings;
        }

        private void OnDisable()
        {
            SettingsManager.Instance.OnSettingsApplied -= LoadUIFromSettings;
        }

        private void PopulateQualityDropdown()
        {
            qualityDropdown.ClearOptions();
            var names = new List<string>(QualitySettings.names);
            qualityDropdown.AddOptions(names);
        }

        private void PopulateResolutionDropdown()
        {
            resolutionDropdown.ClearOptions();
            resolutionOriginalIndices.Clear();

            var allRes = Screen.resolutions;
            // Убираем дубликаты (по ширине, высоте, частоте): берем первую встречу
            var seen = new HashSet<string>();
            var displayOptions = new List<string>();
            for (int i = 0; i < allRes.Length; i++)
            {
                var r = allRes[i];
                string key = $"{r.width}x{r.height}@{r.refreshRate}";
                if (seen.Add(key))
                {
                    displayOptions.Add($"{r.width}x{r.height} {r.refreshRate}Hz");
                    resolutionOriginalIndices.Add(i); // запоминаем оригинальный индекс
                }
            }

            resolutionDropdown.AddOptions(displayOptions);
        }

        // private void PopulateAntiAliasingDropdown()
        // {
        //     antiAliasingDropdown.ClearOptions();
        //     antiAliasingDropdown.AddOptions(new List<string>(aaDisplay));
        // }

        // private void PopulateLanguageDropdown()
        // {
        //     languageDropdown.ClearOptions();
        //     if (languageCodes.Count != languageDisplayNames.Count)
        //     {
        //         Debug.LogWarning("languageCodes и languageDisplayNames разной длины.");
        //     }
        //
        //     languageDropdown.AddOptions(new List<string>(languageDisplayNames));
        // }

        private void AddListeners()
        {
            voiceChatSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            slotsSlider.onValueChanged.AddListener(OnSlotsVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            // fpsInputField.onEndEdit.AddListener(OnFPSLimitEdited);
            // effectsToggle.onValueChanged.AddListener(OnEffectsToggled);
            // antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);

            cameraSensitivitySlider.onValueChanged.AddListener(OnCameraSensitivityChanged);
            invertCameraToggleOn.onValueChanged.AddListener(OnInvertCameraChanged);

            // languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        private void LoadUIFromSettings()
        {
            var sm = SettingsManager.Instance;

            // Audio
            voiceChatSlider.value = sm.VoiceChatVolume;
            musicSlider.value = sm.MusicVolume;
            slotsSlider.value = sm.SlotsVolume;
            sfxSlider.value = sm.SFXVolume;

            // Graphics
            qualityDropdown.value = Mathf.Clamp(sm.QualityLevel, 0, qualityDropdown.Options.Count - 1);
            qualityDropdown.RefreshShownValue();

            // Resolution: найти отображаемый индекс, соответствующий текущему ResolutionIndex
            int uiResIndex = resolutionOriginalIndices.FindIndex(orig => orig == sm.ResolutionIndex);
            if (uiResIndex >= 0)
                resolutionDropdown.value = uiResIndex;
            else
                resolutionDropdown.value = 0;
            resolutionDropdown.RefreshShownValue();

            // fpsInputField.text = sm.FPSLimit.ToString();
            // effectsToggle.isOn = sm.EffectsEnabled;
            //
            // int aaIndex = Array.IndexOf(aaLevels, sm.AntiAliasingLevel);
            // if (aaIndex < 0) aaIndex = 0;
            // antiAliasingDropdown.value = aaIndex;
            // antiAliasingDropdown.RefreshShownValue();

            // Camera & Controls
            cameraSensitivitySlider.value = sm.CameraSensitivity;
            invertCameraToggleOn.isOn = sm.InvertCamera;
            invertCameraToggleOff.isOn = !sm.InvertCamera;
            
            // Localization
            // int langIndex = languageCodes.IndexOf(sm.LanguageCode);
            // if (langIndex >= 0 && langIndex < languageDropdown.options.Count)
            //     languageDropdown.value = langIndex;
            // else
            //     languageDropdown.value = 0;
            // languageDropdown.RefreshShownValue();
        }

        #region UI Callbacks

        private void OnVoiceVolumeChanged(float v)
        {
            SettingsManager.Instance.SetVoiceVolume(v);
            // Немедленно применим звук, если SetVoiceVolume не делает этого (в зависимости от патча)
            AudioManager.Instance.SetVolume("VoiceChat", v);
        }

        private void OnMusicVolumeChanged(float v)
        {
            SettingsManager.Instance.SetMusicVolume(v);
            AudioManager.Instance.SetVolume("Music", v);
        }

        private void OnSlotsVolumeChanged(float v)
        {
            SettingsManager.Instance.SetSlotsVolume(v);
            AudioManager.Instance.SetVolume("Slots", v);
        }

        private void OnSFXVolumeChanged(float v)
        {
            SettingsManager.Instance.SetSFXVolume(v);
            AudioManager.Instance.SetVolume("SFX", v);
        }

        private void OnQualityChanged(int idx)
        {
            SettingsManager.Instance.SetGraphicsQuality(idx);
            QualitySettings.SetQualityLevel(idx); // визуально сразу
        }

        private void OnResolutionChanged(int uiIndex)
        {
            if (uiIndex < 0 || uiIndex >= resolutionOriginalIndices.Count)
                return;
            int originalIndex = resolutionOriginalIndices[uiIndex];
            SettingsManager.Instance.SetResolution(originalIndex);
            // сразу применим
            var res = Screen.resolutions[originalIndex];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        }

        /*private void OnFPSLimitEdited(string str)
        {
            if (int.TryParse(str, out int fps))
            {
                SettingsManager.Instance.SetFPSLimit(fps);
                Application.targetFrameRate = fps;
            }
            else
            {
                // восстановить прежнее корректное
                fpsInputField.text = SettingsManager.Instance.FPSLimit.ToString();
            }
        }*/

        private void OnEffectsToggled(bool enabled)
        {
            SettingsManager.Instance.SetEffectsEnabled(enabled);
            PostProcessingManager.Instance.SetEnabled(enabled);
        }

        private void OnAntiAliasingChanged(int idx)
        {
            int level = aaLevels[Mathf.Clamp(idx, 0, aaLevels.Length - 1)];
            SettingsManager.Instance.SetAntiAliasing(level);
            QualitySettings.antiAliasing = level;
        }

        private void OnCameraSensitivityChanged(float v)
        {
            SettingsManager.Instance.SetCameraSensitivity(v);
        }

        private void OnInvertCameraChanged(bool invert)
        {
            SettingsManager.Instance.SetInvertCamera(invert);
        }

        // private void OnLanguageChanged(int idx)
        // {
        //     if (idx >= 0 && idx < languageCodes.Count)
        //     {
        //         string code = languageCodes[idx];
        //         // Если есть метод установки языка — его нужно раскомментировать / реализовать
        //         // SettingsManager.Instance.SetLanguage(code);
        //         // Пока просто сохраняем
        //         // Дополнительно: здесь можно вызывать локализацию напрямую, если есть система
        //     }
        // }

        #endregion

        private void ApplySettings()
        {
            SettingsManager.Instance.SaveAllSettings();
            SettingsManager.Instance.ApplyAllSettings();
        }

        private void RevertUI()
        {
            SettingsManager.Instance.LoadAllSettings();
            LoadUIFromSettings();
        }

        private void ResetToDefaults()
        {
            SettingsManager.Instance.ResetToDefaults();
        }
    }
}
