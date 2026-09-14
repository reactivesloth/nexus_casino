#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Util
{
    [InitializeOnLoad]
    public static class PlayFromStartingScene
    {
        private const string PLAY_FROM_FIRST_MENU_STR = "Edit/Always Start From Scene 0 &p";

        private static bool PlayFromFirstScene
        {
            get => EditorPrefs.HasKey(PLAY_FROM_FIRST_MENU_STR) && EditorPrefs.GetBool(PLAY_FROM_FIRST_MENU_STR, true);
            set => EditorPrefs.SetBool(PLAY_FROM_FIRST_MENU_STR, value);
        }

        // Флаг чтобы избежать рекурсии при автоматически повторном входе в плеймод
        private static bool s_IsChangingSceneForPlay = false;

        static PlayFromStartingScene()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Поддержать чекбокс в меню при старте
            EditorApplication.delayCall += () => Menu.SetChecked(PLAY_FROM_FIRST_MENU_STR, PlayFromFirstScene);
        }

        [MenuItem(PLAY_FROM_FIRST_MENU_STR, false, 150)]
        private static void PlayFromFirstSceneCheckMenu()
        {
            PlayFromFirstScene = !PlayFromFirstScene;
            Menu.SetChecked(PLAY_FROM_FIRST_MENU_STR, PlayFromFirstScene);

            ShowNotifyOrLog(PlayFromFirstScene ? "Play from scene 0" : "Play from current scene");
        }

        [MenuItem(PLAY_FROM_FIRST_MENU_STR, true)]
        private static bool PlayFromFirstSceneCheckMenuValidate()
        {
            Menu.SetChecked(PLAY_FROM_FIRST_MENU_STR, PlayFromFirstScene);
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!PlayFromFirstScene)
                return;

            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (s_IsChangingSceneForPlay)
                    return;

                if (EditorBuildSettings.scenes.Length == 0)
                {
                    Debug.LogWarning("The scene build list is empty. Can't play from first scene.");
                    return;
                }

                string firstScenePath = EditorBuildSettings.scenes[0].path;
                if (string.IsNullOrEmpty(firstScenePath))
                    return;

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != firstScenePath)
                {
                    // Перехватываем нажатие Play: отменяем, переключаем сцену, и снова запускаем Play.
                    s_IsChangingSceneForPlay = true;

                    // Отменим текущий переход в плеймод
                    EditorApplication.isPlaying = false;

                    // Сохраним текущие изменения, если пользователь не отменит — прервём
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        s_IsChangingSceneForPlay = false;
                        return;
                    }

                    EditorSceneManager.OpenScene(firstScenePath);

                    // Отложенно включим плеймод заново
                    EditorApplication.delayCall += () =>
                    {
                        s_IsChangingSceneForPlay = false;
                        EditorApplication.isPlaying = true;
                    };
                }
            }
        }

        // Этот метод оставляем как fallback, но делаем так, чтобы он не перезагружал сцену 0 если она уже активна.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadFirstSceneAtGameBegins()
        {
            if (!PlayFromFirstScene)
                return;

            if (EditorBuildSettings.scenes.Length == 0)
            {
                Debug.LogWarning("The scene build list is empty. Can't play from first scene.");
                return;
            }

            string firstScenePath = EditorBuildSettings.scenes[0].path;
            if (string.IsNullOrEmpty(firstScenePath))
                return;

            Scene active = SceneManager.GetActiveScene();
            if (active.path == firstScenePath)
                return; // уже первая сцена, не делать двойную загрузку

            SceneManager.LoadScene(0);
        }

        private static void ShowNotifyOrLog(string msg)
        {
            if (Resources.FindObjectsOfTypeAll<SceneView>().Length > 0)
                EditorWindow.GetWindow<SceneView>().ShowNotification(new GUIContent(msg));
            else
                Debug.Log(msg);
        }
    }
}
#endif
