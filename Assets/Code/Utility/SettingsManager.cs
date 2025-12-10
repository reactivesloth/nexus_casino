using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SocialPlatforms;

namespace Code.Utility
{
    public sealed class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // Храним «человеческие» проценты 0..100
        private const float DefaultVoiceChatVolume = 50f;
        private const float DefaultMusicVolume     = 30f;
        private const float DefaultSlotsVolume     = 30f;
        private const float DefaultSFXVolume       = 30f;
        private int DefaultGraphicsQuality => QualitySettings.GetQualityLevel();
        private const bool  DefaultEffectsEnabled  = true;
        private const int   DefaultAntiAliasing    = 2;
        private const float DefaultCameraSensitivity = 40f;
        private const bool  DefaultInvertCamera    = false;

        [Header("Audio (0..100)")]
        public float VoiceChatVolume { get; private set; }
        public float MusicVolume     { get; private set; }
        public float SlotsVolume     { get; private set; }
        public float SFXVolume       { get; private set; }

        [Header("Graphics")]
        public int  QualityLevel     { get; private set; }
        public int  FPSLimit         { get; private set; }
        public bool EffectsEnabled   { get; private set; }
        public int  AntiAliasingLevel{ get; private set; }

        [Header("Camera & Controls")]
        public float CameraSensitivity { get; private set; }
        public bool  InvertCamera      { get; private set; }

        [Header("Localization")]
        public Locale Localization { get; private set; }

        public event Action OnSettingsApplied;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            LoadAllSettings();
        }

        #region Load / Save

        public void LoadAllSettings()
        {
            VoiceChatVolume = PlayerPrefs.GetFloat("VoiceVolume",   DefaultVoiceChatVolume);
            MusicVolume     = PlayerPrefs.GetFloat("MusicVolume",   DefaultMusicVolume);
            SlotsVolume     = PlayerPrefs.GetFloat("SlotsVolume",   DefaultSlotsVolume);
            SFXVolume       = PlayerPrefs.GetFloat("SFXVolume",     DefaultSFXVolume);

            QualityLevel      = PlayerPrefs.GetInt("GraphicsQuality", DefaultGraphicsQuality);
#if UNITY_IOS || UNITY_ANDROID
            FPSLimit          = PlayerPrefs.GetInt("FPSLimit",         0);
#else
            FPSLimit          = PlayerPrefs.GetInt("FPSLimit",         2);
#endif
            EffectsEnabled    = PlayerPrefs.GetInt("EffectsEnabled",   DefaultEffectsEnabled ? 1 : 0) == 1;
            AntiAliasingLevel = PlayerPrefs.GetInt("AntiAliasingLevel", DefaultAntiAliasing);

            CameraSensitivity = PlayerPrefs.GetFloat("CameraSensitivity", DefaultCameraSensitivity);
            InvertCamera      = PlayerPrefs.GetInt("InvertCamera", DefaultInvertCamera ? 1 : 0) == 1;

            Localization      = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(PlayerPrefs.GetString("Language", LocalizationSettings.SelectedLocale.Identifier.Code)));

            ApplyAllSettings();
        }

        public void SaveAllSettings()
        {
            PlayerPrefs.SetFloat("VoiceVolume",   VoiceChatVolume);
            PlayerPrefs.SetFloat("MusicVolume",   MusicVolume);
            PlayerPrefs.SetFloat("SlotsVolume",   SlotsVolume);
            PlayerPrefs.SetFloat("SFXVolume",     SFXVolume);

            PlayerPrefs.SetInt("GraphicsQuality", QualityLevel);
            PlayerPrefs.SetInt("FPSLimit",        FPSLimit);
            PlayerPrefs.SetInt("EffectsEnabled",  EffectsEnabled ? 1 : 0);
            PlayerPrefs.SetInt("AntiAliasingLevel", AntiAliasingLevel);

            PlayerPrefs.SetFloat("CameraSensitivity", CameraSensitivity);
            PlayerPrefs.SetInt("InvertCamera",       InvertCamera ? 1 : 0);

            PlayerPrefs.SetString("Language", LocalizationSettings.SelectedLocale.Identifier.Code);
            PlayerPrefs.Save();
        }

        public void ResetToDefaults()
        {
            VoiceChatVolume = DefaultVoiceChatVolume;
            MusicVolume     = DefaultMusicVolume;
            SlotsVolume     = DefaultSlotsVolume;
            SFXVolume       = DefaultSFXVolume;

            QualityLevel      = DefaultGraphicsQuality;
#if UNITY_IOS || UNITY_ANDROID
            FPSLimit          = 0;
#else
            FPSLimit          = 2;
#endif
            EffectsEnabled    = DefaultEffectsEnabled;
            AntiAliasingLevel = DefaultAntiAliasing;

            CameraSensitivity = DefaultCameraSensitivity;
            InvertCamera      = DefaultInvertCamera;

            Localization      = LocalizationSettings.AvailableLocales.GetLocale(LocalizationSettings.SelectedLocale.LocaleName);

            SaveAllSettings();
            ApplyAllSettings();
        }

        #endregion

        #region Apply

        private static float PercentTo01(float v) => Mathf.Clamp01(v / 100f);

        public void ApplyAllSettings()
        {
            // Audio
            var am = AudioManager.Instance;
            if (am != null)
            {
                am.SetVolume("VoiceChat", PercentTo01(VoiceChatVolume));
                am.SetVolume("Music",     PercentTo01(MusicVolume));
                am.SetVolume("Slots",     PercentTo01(SlotsVolume));
                am.SetVolume("SFX",       PercentTo01(SFXVolume));
            }

            LocalizationSettings.SelectedLocale = Localization;
            
            // Graphics
            QualitySettings.SetQualityLevel(QualityLevel);
            Application.targetFrameRate = FPSLimit switch
            {
                0 => 15,
                1 => 30,
                2 => 60,
                _ => 0
            };
            
            QualitySettings.antiAliasing = Mathf.Max(0, AntiAliasingLevel);;

            // PostFX
            var pp = PostProcessingManager.Instance;
            if (pp != null) pp.SetEnabled(EffectsEnabled);

            OnSettingsApplied?.Invoke();
        }

        #endregion

        #region Public setters (UI)

        public void SetVoiceVolume(float percent)   { VoiceChatVolume = percent; PlayerPrefs.SetFloat("VoiceVolume", percent);   AudioManager.Instance?.SetVolume("VoiceChat", PercentTo01(percent)); }
        public void SetMusicVolume(float percent)   { MusicVolume     = percent; PlayerPrefs.SetFloat("MusicVolume", percent);   AudioManager.Instance?.SetVolume("Music",     PercentTo01(percent)); }
        public void SetSlotsVolume(float percent)   { SlotsVolume     = percent; PlayerPrefs.SetFloat("SlotsVolume", percent);   AudioManager.Instance?.SetVolume("Slots",     PercentTo01(percent)); }
        public void SetSFXVolume(float percent)     { SFXVolume       = percent; PlayerPrefs.SetFloat("SFXVolume",   percent);   AudioManager.Instance?.SetVolume("SFX",       PercentTo01(percent)); }

        public void SetGraphicsQuality(int level)   { QualityLevel      = level; PlayerPrefs.SetInt("GraphicsQuality", level);   QualitySettings.SetQualityLevel(level); }

        public void SetFPSLimit(int fpsLevel)
        {
            FPSLimit = fpsLevel;
            PlayerPrefs.SetInt("FPSLimit", fpsLevel);
            Application.targetFrameRate = fpsLevel switch
            {
                0 => 15,
                1 => 30,
                2 => 60,
                _ => 0
            };
        }

        public void SetEffectsEnabled(bool enabled) { EffectsEnabled    = enabled; PlayerPrefs.SetInt("EffectsEnabled", enabled ? 1 : 0); PostProcessingManager.Instance?.SetEnabled(enabled); }
        public void SetAntiAliasing(int level)      { AntiAliasingLevel = level; PlayerPrefs.SetInt("AntiAliasingLevel", level); QualitySettings.antiAliasing = Mathf.Max(0, level); }

        public void SetCameraSensitivity(float sens) { CameraSensitivity = sens; PlayerPrefs.SetFloat("CameraSensitivity", sens); }
        public void SetInvertCamera(bool invert)     { InvertCamera      = invert; PlayerPrefs.SetInt("InvertCamera", invert ? 1 : 0); }

        public void SetLanguage(Locale locale)         { Localization = locale; PlayerPrefs.SetString("Language", LocalizationSettings.SelectedLocale.Identifier.Code);}

        #endregion
    }
}
