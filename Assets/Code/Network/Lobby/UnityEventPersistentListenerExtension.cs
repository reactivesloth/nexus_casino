using UnityEngine.Events;

namespace EOSLobby
{
    /// <summary>
    /// Аккуратная обёртка над добавлением/удалением persistent listeners.
    /// В Editor использует UnityEventTools, в рантайме — обычные AddListener/RemoveListener.
    /// </summary>
    public static class UnityEventPersistentListenerExtension
    {
        public static void AddPersistentListener(this UnityEvent unityEvent, UnityAction call)
        {
            if (unityEvent == null || call == null) return;
#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.AddPersistentListener(unityEvent, call);
#else
            unityEvent.AddListener(call);
#endif
        }

        public static void AddPersistentListener<T0>(this UnityEvent<T0> unityEvent, UnityAction<T0> call)
        {
            if (unityEvent == null || call == null) return;
#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.AddPersistentListener(unityEvent, call);
#else
            unityEvent.AddListener(call);
#endif
        }

        public static void RemovePersistentListener(this UnityEvent unityEvent, UnityAction call)
        {
            if (unityEvent == null || call == null) return;
#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(unityEvent, call);
#else
            unityEvent.RemoveListener(call);
#endif
        }

        public static void RemovePersistentListener<T0>(this UnityEvent<T0> unityEvent, UnityAction<T0> call)
        {
            if (unityEvent == null || call == null) return;
#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(unityEvent, call);
#else
            unityEvent.RemoveListener(call);
#endif
        }
    }
}