using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Code.Utility
{
    public static class LocalizationHelper
    {
        private const string LocalizationTableName = "LocalizationTable";
        private const int InitializationTimeoutSeconds = 10;
        private const bool LogInitialization = true;

        // Словарь для хранения зарегистрированных текстовых компонентов
        private static readonly Dictionary<TMP_Text, LocalizedTextData> RegisteredTexts = new Dictionary<TMP_Text, LocalizedTextData>();
        private static bool _isLocaleListenerRegistered = false;

        // Класс для хранения данных о локализованном тексте
        private class LocalizedTextData
        {
            public string TableKey;
            public Dictionary<string, object> Parameters;
            public Action OnComplete;
        }

        private static async Task EnsureInitializedAsync()
        {
            var initOp = LocalizationSettings.InitializationOperation;

            if (initOp.IsDone)
                return;

            if (LogInitialization)
                Debug.Log("[LocalizationHelper] Waiting for Localization initialization...");

            var finished = await Task.WhenAny(initOp.Task, Task.Delay(TimeSpan.FromSeconds(InitializationTimeoutSeconds)));
            if (finished != initOp.Task)
            {
                Debug.LogWarning("[LocalizationHelper] Localization initialization timeout!");
            }
            else
            {
                if (LogInitialization)
                    Debug.Log("[LocalizationHelper] Localization initialization complete.");
            }
        }

        // Регистрация обработчика смены локали
        private static void EnsureLocaleChangeListener()
        {
            if (_isLocaleListenerRegistered)
                return;

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _isLocaleListenerRegistered = true;

            if (LogInitialization)
                Debug.Log("[LocalizationHelper] Locale change listener registered.");
        }

        // Обработчик смены локали
        private static void OnLocaleChanged(Locale newLocale)
        {
            if (LogInitialization)
                Debug.Log($"[LocalizationHelper] Locale changed to: {newLocale.LocaleName}. Updating {RegisteredTexts.Count} text components.");

            // Создаём копию списка для безопасного итерирования
            var textsToUpdate = new List<TMP_Text>(RegisteredTexts.Keys);

            foreach (var textComponent in textsToUpdate)
            {
                // Проверка на null - компонент мог быть уничтожен
                if (textComponent == null || textComponent.gameObject == null)
                {
                    RegisteredTexts.Remove(textComponent);
                    continue;
                }

                var data = RegisteredTexts[textComponent];
                UpdateTextComponent(textComponent, data);
            }
        }

        // Внутренний метод для обновления текстового компонента
        private static async void UpdateTextComponent(TMP_Text textComponent, LocalizedTextData data)
        {
            if (textComponent == null)
                return;

            await EnsureInitializedAsync();

            var localizedString = new LocalizedString(LocalizationTableName, data.TableKey);

            // Добавляем параметры, если они есть
            if (data.Parameters != null)
            {
                foreach (var kvp in data.Parameters)
                {
                    localizedString.Add(kvp.Key, new StringVariable { Value = kvp.Value.ToString() });
                }
            }

            string result = await localizedString.GetLocalizedStringAsync().Task;

            // Финальная проверка на null перед установкой текста
            if (textComponent != null)
            {
                textComponent.text = result;
                data.OnComplete?.Invoke();
            }
        }

        // Регистрация текстового компонента для автообновления
        private static void RegisterTextComponent(TMP_Text textComponent, string tableKey, 
            Dictionary<string, object> parameters, Action onComplete)
        {
            if (textComponent == null)
                return;

            EnsureLocaleChangeListener();

            RegisteredTexts[textComponent] = new LocalizedTextData
            {
                TableKey = tableKey,
                Parameters = parameters,
                OnComplete = onComplete
            };
        }

        // ========== СИНХРОННЫЕ МЕТОДЫ ДЛЯ ПОЛУЧЕНИЯ СТРОК ==========

        /// <summary>
        /// Синхронно получает локализованную строку по ключу
        /// </summary>
        public static string GetLocalizedString(string tableKey)
        {
            if (string.IsNullOrEmpty(tableKey))
            {
                Debug.LogWarning("[LocalizationHelper] Table key is null or empty!");
                return string.Empty;
            }

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);
            string result = localizedString.GetLocalizedString();

            return result ?? string.Empty;
        }

        /// <summary>
        /// Синхронно получает локализованную строку с одним параметром
        /// </summary>
        public static string GetLocalizedString(string tableKey, string paramName, object paramValue)
        {
            if (string.IsNullOrEmpty(tableKey))
            {
                Debug.LogWarning("[LocalizationHelper] Table key is null or empty!");
                return string.Empty;
            }

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);
            localizedString.Add(paramName, new StringVariable { Value = paramValue.ToString() });

            string result = localizedString.GetLocalizedString();

            return result ?? string.Empty;
        }

        /// <summary>
        /// Синхронно получает локализованную строку с несколькими параметрами
        /// </summary>
        public static string GetLocalizedString(string tableKey, params (string name, object value)[] parameters)
        {
            if (string.IsNullOrEmpty(tableKey))
            {
                Debug.LogWarning("[LocalizationHelper] Table key is null or empty!");
                return string.Empty;
            }

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);

            if (parameters != null)
            {
                foreach (var (name, value) in parameters)
                {
                    localizedString.Add(name, new StringVariable { Value = value.ToString() });
                }
            }

            string result = localizedString.GetLocalizedString();

            return result ?? string.Empty;
        }

        // ========== АСИНХРОННЫЕ МЕТОДЫ ДЛЯ ПОЛУЧЕНИЯ СТРОК ==========

        /// <summary>
        /// Асинхронно получает локализованную строку по ключу
        /// </summary>
        public static async Task<string> GetLocalizedStringAsync(string tableKey)
        {
            if (string.IsNullOrEmpty(tableKey))
            {
                Debug.LogWarning("[LocalizationHelper] Table key is null or empty!");
                return string.Empty;
            }

            await EnsureInitializedAsync();

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);
            string result = await localizedString.GetLocalizedStringAsync().Task;

            return result ?? string.Empty;
        }

        /// <summary>
        /// Асинхронно получает локализованную строку с одним параметром
        /// </summary>
        public static async Task<string> GetLocalizedStringAsync(string tableKey, string paramName, object paramValue)
        {
            if (string.IsNullOrEmpty(tableKey))
            {
                Debug.LogWarning("[LocalizationHelper] Table key is null or empty!");
                return string.Empty;
            }

            await EnsureInitializedAsync();

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);
            localizedString.Add(paramName, new StringVariable { Value = paramValue.ToString() });

            string result = await localizedString.GetLocalizedStringAsync().Task;

            return result ?? string.Empty;
        }

        /// <summary>
        /// Асинхронно получает локализованную строку с несколькими параметрами
        /// </summary>
        public static async Task<string> GetLocalizedStringAsync(string tableKey, params (string name, object value)[] parameters)
        {
            if (string.IsNullOrEmpty(tableKey))
            {
                Debug.LogWarning("[LocalizationHelper] Table key is null or empty!");
                return string.Empty;
            }

            await EnsureInitializedAsync();

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);

            if (parameters != null)
            {
                foreach (var (name, value) in parameters)
                {
                    localizedString.Add(name, new StringVariable { Value = value.ToString() });
                }
            }

            string result = await localizedString.GetLocalizedStringAsync().Task;

            return result ?? string.Empty;
        }

        // ========== ОРИГИНАЛЬНЫЕ МЕТОДЫ ДЛЯ УСТАНОВКИ ТЕКСТА ==========

        public static async void SetLocalizedTextAsync(TMP_Text textComponent, string tableKey, Action onComplete = null)
        {
            if (textComponent == null || string.IsNullOrEmpty(tableKey))
            {
                Debug.LogWarning("[LocalizationHelper] TMP_Text component is null!");
                return;
            }

            await EnsureInitializedAsync();

            // Регистрируем для автообновления
            RegisterTextComponent(textComponent, tableKey, null, onComplete);

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);
            string result = await localizedString.GetLocalizedStringAsync().Task;

            if (textComponent != null)
            {
                textComponent.text = result;
                onComplete?.Invoke();
            }
        }

        public static async void SetLocalizedTextAsync(TMP_Text textComponent, string tableKey,
            string paramName, object paramValue, Action onComplete = null)
        {
            if (textComponent == null)
            {
                Debug.LogWarning("[LocalizationHelper] TMP_Text component is null!");
                return;
            }

            await EnsureInitializedAsync();

            var parameters = new Dictionary<string, object> { { paramName, paramValue } };
            RegisterTextComponent(textComponent, tableKey, parameters, onComplete);

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);
            localizedString.Add(paramName, new StringVariable { Value = paramValue.ToString() });

            string result = await localizedString.GetLocalizedStringAsync().Task;

            if (textComponent != null)
            {
                textComponent.text = result;
                onComplete?.Invoke();
            }
        }

        public static async void SetLocalizedTextAsync(TMP_Text textComponent, string tableKey,
            params (string name, object value)[] parameters)
        {
            await SetLocalizedTextAsyncTask(textComponent, tableKey, null, parameters);
        }

        public static async void SetLocalizedTextAsync(TMP_Text textComponent, string tableKey,
            Action onComplete, params (string name, object value)[] parameters)
        {
            await SetLocalizedTextAsyncTask(textComponent, tableKey, onComplete, parameters);
        }

        private static async Task SetLocalizedTextAsyncTask(TMP_Text textComponent, string tableKey,
            Action onComplete, params (string name, object value)[] parameters)
        {
            if (textComponent == null)
            {
                Debug.LogWarning("[LocalizationHelper] TMP_Text component is null!");
                return;
            }

            await EnsureInitializedAsync();

            // Создаём словарь параметров для регистрации
            var paramDict = new Dictionary<string, object>();
            if (parameters != null)
            {
                foreach (var (name, value) in parameters)
                {
                    paramDict[name] = value;
                }
            }

            RegisterTextComponent(textComponent, tableKey, paramDict, onComplete);

            var localizedString = new LocalizedString(LocalizationTableName, tableKey);

            foreach (var (name, value) in parameters)
            {
                localizedString.Add(name, new StringVariable { Value = value.ToString() });
            }

            string result = await localizedString.GetLocalizedStringAsync().Task;

            if (textComponent != null)
            {
                textComponent.text = result;
                onComplete?.Invoke();
            }
        }

        // ========== УТИЛИТЫ ==========

        /// <summary>
        /// Отменяет автоматическое обновление для конкретного текстового компонента
        /// </summary>
        public static void UnregisterText(TMP_Text textComponent)
        {
            if (textComponent != null && RegisteredTexts.ContainsKey(textComponent))
            {
                RegisteredTexts.Remove(textComponent);
                if (LogInitialization)
                    Debug.Log($"[LocalizationHelper] Unregistered text component: {textComponent.name}");
            }
        }

        /// <summary>
        /// Обновляет параметр для уже зарегистрированного компонента
        /// </summary>
        public static void UpdateParameter(TMP_Text textComponent, string paramName, object paramValue)
        {
            if (textComponent == null || !RegisteredTexts.ContainsKey(textComponent))
                return;

            var data = RegisteredTexts[textComponent];
            if (data.Parameters == null)
                data.Parameters = new Dictionary<string, object>();

            data.Parameters[paramName] = paramValue;
            UpdateTextComponent(textComponent, data);
        }

        /// <summary>
        /// Очищает все зарегистрированные компоненты (полезно при смене сцены)
        /// </summary>
        public static void ClearAllRegistrations()
        {
            RegisteredTexts.Clear();
            if (LogInitialization)
                Debug.Log("[LocalizationHelper] All text component registrations cleared.");
        }

        /// <summary>
        /// Возвращает количество зарегистрированных компонентов
        /// </summary>
        public static int GetRegisteredCount()
        {
            // Очищаем null-ссылки перед подсчётом
            var nullKeys = new List<TMP_Text>();
            foreach (var key in RegisteredTexts.Keys)
            {
                if (key == null)
                    nullKeys.Add(key);
            }
            foreach (var key in nullKeys)
            {
                RegisteredTexts.Remove(key);
            }

            return RegisteredTexts.Count;
        }
    }
}