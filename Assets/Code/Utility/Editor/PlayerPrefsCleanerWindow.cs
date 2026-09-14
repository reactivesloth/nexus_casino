#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PlayerPrefsCleanerWindow : EditorWindow
{
    private string keyToDelete = "";

    [MenuItem("Tools/PlayerPrefs Cleaner %#c", priority = 100)] // Ctrl+Shift+C
    public static void OpenWindow()
    {
        var w = GetWindow<PlayerPrefsCleanerWindow>("PlayerPrefs Cleaner");
        w.minSize = new Vector2(350, 180);
    }

    [MenuItem("Tools/Clear All PlayerPrefs", false, 101)]
    private static void ClearAllMenu()
    {
        if (!EditorUtility.DisplayDialog("Подтверждение очистки",
            "Это удалит **все** PlayerPrefs. Отменить будет нельзя. Продолжить?", "Да", "Отмена"))
            return;

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[PlayerPrefsCleaner] Все PlayerPrefs удалены.");
    }

    private void OnGUI()
    {
        GUILayout.Label("PlayerPrefs Cleaner", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Удалить конкретный ключ:", EditorStyles.label);
        EditorGUILayout.BeginHorizontal();
        keyToDelete = EditorGUILayout.TextField(keyToDelete);
        if (GUILayout.Button("Удалить", GUILayout.Width(90)))
        {
            if (string.IsNullOrEmpty(keyToDelete))
            {
                EditorUtility.DisplayDialog("Ошибка", "Ключ не может быть пустым.", "Ок");
            }
            else
            {
                if (!EditorUtility.DisplayDialog("Подтверждение",
                    $"Удалить PlayerPrefs ключ '{keyToDelete}'?", "Да", "Нет"))
                    return;

                if (PlayerPrefs.HasKey(keyToDelete))
                {
                    PlayerPrefs.DeleteKey(keyToDelete);
                    PlayerPrefs.Save();
                    Debug.Log($"[PlayerPrefsCleaner] Удалён ключ '{keyToDelete}'.");
                }
                else
                {
                    EditorUtility.DisplayDialog("Не найдено", $"Ключ '{keyToDelete}' не найден.", "Ок");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        if (GUILayout.Button("Удалить все PlayerPrefs"))
        {
            if (EditorUtility.DisplayDialog("Подтверждение очистки",
                "Это удалит все PlayerPrefs. Продолжить?", "Да", "Отмена"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.Log("[PlayerPrefsCleaner] Все PlayerPrefs удалены.");
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Unity не предоставляет публичный API для перечисления всех ключей PlayerPrefs. " +
            "Если тебе нужно отслеживать, какие ключи используются, заведите центральный реестр ключей в коде или сохраняй список вручную.", 
            MessageType.Info);
    }

    // Удобная горячая клавиша для очистки всех (Ctrl+Shift+A)
    [MenuItem("Tools/Clear All PlayerPrefs %#a")]
    private static void ClearAllHotkey()
    {
        ClearAllMenu();
    }
}
#endif
