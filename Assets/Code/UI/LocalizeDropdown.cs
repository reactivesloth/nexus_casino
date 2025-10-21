using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Code.UI
{
    public class LocalizeDropdown : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown dropdown;
        
        private void OnValidate()
        {
            dropdown ??= GetComponent<TMP_Dropdown>();
        }

        private async void Start()
        {
            await LocalizationSettings.InitializationOperation.Task;
            InitializeDropdown();
        }

        private void InitializeDropdown()
        {
            // Очищаем текущие опции
            dropdown.ClearOptions();

            // Получаем доступные локали
            var locales = LocalizationSettings.AvailableLocales.Locales;
            var localeNames = new List<string>();

            foreach (var locale in locales)
            {
                localeNames.Add(locale.LocaleName);
            }

            // Добавляем опции в dropdown
            dropdown.AddOptions(localeNames);

            // Устанавливаем текущую локаль как выбранную
            var currentLocale = LocalizationSettings.SelectedLocale;
            int currentIndex = locales.IndexOf(currentLocale);
            dropdown.value = currentIndex >= 0 ? currentIndex : 0;

            // Подписываемся на изменение выбора
            dropdown.onValueChanged.AddListener(OnDropdownValueChanged);

            // Подписываемся на смену локали, чтобы обновить dropdown при внешней смене языка
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDropdownValueChanged(int index)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales[index];
            LocalizationSettings.SelectedLocale = locale;
        }

        private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            int index = locales.IndexOf(locale);
            
            if (index >= 0)
            {
                dropdown.SetValueWithoutNotify(index);
            }
        }

        private void OnDestroy()
        {
            if (dropdown != null)
            {
                dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            }

            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }
    }
}
