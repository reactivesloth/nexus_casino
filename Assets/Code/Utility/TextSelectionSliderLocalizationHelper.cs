using System.Collections.Generic;
using System.Linq;
using Ricimi;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Code.Utility
{
    [RequireComponent(typeof(TextSelectionSlider))]
    public class TextSelectionSliderLocalizationHelper : MonoBehaviour
    {
        [SerializeField] private TextSelectionSlider textSelectionSlider;
        [SerializeField] private List<string> optionsKeys = new();
        
        private void OnValidate()
        {
            textSelectionSlider ??= GetComponent<TextSelectionSlider>();
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocalizationChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocalizationChanged;
        }

        public void InitKeys(List<string> keys)
        {
            optionsKeys.Clear();
            optionsKeys = new List<string>(keys);
            RefreshLocalizedOptions();
        }

        private void OnLocalizationChanged(Locale newLocale)
        {
            RefreshLocalizedOptions();
        }

        private void RefreshLocalizedOptions()
        {
            Debug.Log(optionsKeys.Count);
            textSelectionSlider.Options = optionsKeys.Select(LocalizationHelper.GetLocalizedString).ToList();
            textSelectionSlider.RefreshShownValue();
        }
    }
}