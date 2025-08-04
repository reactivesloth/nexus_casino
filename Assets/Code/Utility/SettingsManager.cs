using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
// using UnityEngine.Localization.Settings; // Раскомментируй если подключена система локализации

namespace Code.Utility
{
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // --- Defaults ---
        private const float DefaultVoiceChatVolume = 50f;
        private const float DefaultMusicVolume = 30f;
        private const float DefaultSlotsVolume = 30f;
        private const float DefaultSFXVolume = 30f;
        private int DefaultGraphicsQuality => QualitySettings.GetQualityLevel();
        private int DefaultResolutionIndex => GetDefaultRes ();

        private int GetDefaultRes()
        {
            var allRes = Screen.resolutions;
            // Убираем дубликаты (по ширине, высоте, частоте): берем первую встречу
            var seen = new HashSet<string>();
            var displayOptions = new List<string>();
            List<int> resolutionOriginalIndices  = new List<int>();
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

#if UNITY_ANDROID
            return resolutionOriginalIndices.Reverse();
#endif
            return resolutionOriginalIndices[^1];
        }

        private const int DefaultFPSLimit = 60;
        private const bool DefaultEffectsEnabled = true;
        private const int DefaultAntiAliasingLevel = 2;
        private const float DefaultCameraSensitivity = 100f;
        private const bool DefaultInvertCamera = false;
        private const string DefaultLanguageCode = "en";

        [Header("Audio")] 
        public float VoiceChatVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public float SlotsVolume { get; private set; }
        public float SFXVolume { get; private set; }

        [Header("Graphics")] 
        public int QualityLevel { get; private set; }
        public int ResolutionIndex { get; private set; }
        public int FPSLimit { get; private set; }
        public bool EffectsEnabled { get; private set; }
        public int AntiAliasingLevel { get; private set; }

        [Header("Camera & Controls")] 
        public float CameraSensitivity { get; private set; }
        public bool InvertCamera { get; private set; }

        [Header("Localization")] 
        public string LanguageCode { get; private set; }

        public event Action? OnSettingsApplied;

        private void Awake()
        {
            Instance = this;
            LoadAllSettings(); // внутри ApplyAllSettings вызывается
        }

        #region Load/Save

        public void LoadAllSettings()
        {
            VoiceChatVolume = PlayerPrefs.GetFloat("VoiceVolume", DefaultVoiceChatVolume);
            MusicVolume = PlayerPrefs.GetFloat("MusicVolume", DefaultMusicVolume);
            SlotsVolume = PlayerPrefs.GetFloat("SlotsVolume", DefaultSlotsVolume);
            SFXVolume = PlayerPrefs.GetFloat("SFXVolume", DefaultSFXVolume);

            QualityLevel = PlayerPrefs.GetInt("GraphicsQuality", DefaultGraphicsQuality);
            MaterialVariantSwitcher msv = FindObjectOfType<MaterialVariantSwitcher>();
            msv.SwitchMode(QualityLevel < 2);
            
            ResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", DefaultResolutionIndex);
            FPSLimit = PlayerPrefs.GetInt("FPSLimit", DefaultFPSLimit);
            EffectsEnabled = PlayerPrefs.GetInt("EffectsEnabled", DefaultEffectsEnabled ? 1 : 0) == 1;
            AntiAliasingLevel = PlayerPrefs.GetInt("AntiAliasingLevel", DefaultAntiAliasingLevel);

            CameraSensitivity = PlayerPrefs.GetFloat("CameraSensitivity", DefaultCameraSensitivity);
            InvertCamera = PlayerPrefs.GetInt("InvertCamera", DefaultInvertCamera ? 1 : 0) == 1;
            LanguageCode = PlayerPrefs.GetString("Language", DefaultLanguageCode);

            ApplyAllSettings(); // сразу применяем загруженное состояние
        }

        public void SaveAllSettings()
        {
            PlayerPrefs.SetFloat("VoiceVolume", VoiceChatVolume);
            PlayerPrefs.SetFloat("MusicVolume", MusicVolume);
            PlayerPrefs.SetFloat("SlotsVolume", SlotsVolume);
            PlayerPrefs.SetFloat("SFXVolume", SFXVolume);

            PlayerPrefs.SetInt("GraphicsQuality", QualityLevel);
            PlayerPrefs.SetInt("ResolutionIndex", ResolutionIndex);
            PlayerPrefs.SetInt("FPSLimit", FPSLimit);
            PlayerPrefs.SetInt("EffectsEnabled", EffectsEnabled ? 1 : 0);
            PlayerPrefs.SetInt("AntiAliasingLevel", AntiAliasingLevel);

            PlayerPrefs.SetFloat("CameraSensitivity", CameraSensitivity);
            PlayerPrefs.SetInt("InvertCamera", InvertCamera ? 1 : 0);

            PlayerPrefs.SetString("Language", LanguageCode);

            PlayerPrefs.Save();
        }

        public void ResetToDefaults()
        {
            VoiceChatVolume = DefaultVoiceChatVolume;
            MusicVolume = DefaultMusicVolume;
            SlotsVolume = DefaultSlotsVolume;
            SFXVolume = DefaultSFXVolume;

            QualityLevel = DefaultGraphicsQuality;
            //ResolutionIndex = DefaultResolutionIndex;
            FPSLimit = DefaultFPSLimit;
            EffectsEnabled = DefaultEffectsEnabled;
            AntiAliasingLevel = DefaultAntiAliasingLevel;

            CameraSensitivity = DefaultCameraSensitivity;
            InvertCamera = DefaultInvertCamera;

            LanguageCode = DefaultLanguageCode;

            SaveAllSettings();
            ApplyAllSettings();
        }

        #endregion

        #region Apply

        public void ApplyAllSettings()
        {
            // Audio
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetVolume("VoiceChat", VoiceChatVolume);
                AudioManager.Instance.SetVolume("Music", MusicVolume);
                AudioManager.Instance.SetVolume("Slots", SlotsVolume);
                AudioManager.Instance.SetVolume("SFX", SFXVolume);
            }

            // Graphics & display
            QualitySettings.SetQualityLevel(QualityLevel);
            ApplyResolution();
            Application.targetFrameRate = FPSLimit;
            QualitySettings.antiAliasing = AntiAliasingLevel;

            if (PostProcessingManager.Instance != null)
                PostProcessingManager.Instance.SetEnabled(EffectsEnabled);

            // Localization (если подключена)
            // LocalizationSettings.SelectedLocale =
            //     LocalizationSettings.AvailableLocales.Locales.Find(locale => locale.Identifier.Code == LanguageCode);

            OnSettingsApplied?.Invoke();
        }

        private void ApplyResolution()
        {
            List<Resolution> resolutions = new List<Resolution>(Screen.resolutions);
#if UNITY_ANDROID
            resolutions.Reverse();
#endif
            
            if (ResolutionIndex >= 0 && ResolutionIndex < resolutions.Count)
            {
                var res = resolutions[ResolutionIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }
        }

        #endregion

        #region Public Setters (для UI и прочего)

        public void SetVoiceVolume(float v)
        {
            VoiceChatVolume = v;
            PlayerPrefs.SetFloat("VoiceVolume", v);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetVolume("VoiceChat", v);
        }

        public void SetMusicVolume(float v)
        {
            MusicVolume = v;
            PlayerPrefs.SetFloat("MusicVolume", v);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetVolume("MusicVolume", v);
        }

        public void SetSlotsVolume(float v)
        {
            SlotsVolume = v;
            PlayerPrefs.SetFloat("SlotsVolume", v);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetVolume("SlotsVolume", v);
        }

        public void SetSFXVolume(float v)
        {
            SFXVolume = v;
            PlayerPrefs.SetFloat("SFXVolume", v);
            if (AudioManager.Instance != null)
                AudioManager.Instance.SetVolume("SFXVolume", v);
        }

        public void SetGraphicsQuality(int level)
        {
            QualityLevel = level;
            PlayerPrefs.SetInt("GraphicsQuality", level);
            QualitySettings.SetQualityLevel(level);
            MaterialVariantSwitcher msv = FindObjectOfType<MaterialVariantSwitcher>();
            msv.SwitchMode(QualityLevel < 2);
        }

        public void SetResolution(int index)
        {
            ResolutionIndex = index;
            PlayerPrefs.SetInt("ResolutionIndex", index);
            ApplyResolution();
        }

        public void SetFPSLimit(int fps)
        {
            FPSLimit = fps;
            PlayerPrefs.SetInt("FPSLimit", fps);
            Application.targetFrameRate = fps;
        }

        public void SetEffectsEnabled(bool enabled)
        {
            EffectsEnabled = enabled;
            PlayerPrefs.SetInt("EffectsEnabled", enabled ? 1 : 0);
            if (PostProcessingManager.Instance != null)
                PostProcessingManager.Instance.SetEnabled(enabled);
        }

        public void SetAntiAliasing(int level)
        {
            AntiAliasingLevel = level;
            PlayerPrefs.SetInt("AntiAliasingLevel", level);
            QualitySettings.antiAliasing = level;
        }

        public void SetCameraSensitivity(float sens)
        {
            CameraSensitivity = sens;
            PlayerPrefs.SetFloat("CameraSensitivity", sens);
        }

        public void SetInvertCamera(bool invert)
        {
            InvertCamera = invert;
            PlayerPrefs.SetInt("InvertCamera", invert ? 1 : 0);
        }

        public void SetLanguage(string code)
        {
            LanguageCode = code;
            PlayerPrefs.SetString("Language", code);
            // Применение локали, если есть система:
            // LocalizationSettings.SelectedLocale =
            //     LocalizationSettings.AvailableLocales.Locales.Find(locale => locale.Identifier.Code == code);
        }

        #endregion
    }
}
