using UnityEngine;

namespace Code.Utility
{
    public class DisableLogsOnSceneLoad : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableLogs()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.logEnabled = false; // отключаем все логи
#endif
        }
    }
}