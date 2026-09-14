using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Code.Utility
{
    public sealed class LowMemoryOptimizer : MonoBehaviour
    {
        [SerializeField] private float unloadResourcesInterval = 60f;

        private void Awake()
        {
            if (unloadResourcesInterval > 0)
                InvokeRepeating(nameof(TryCleanup), unloadResourcesInterval, unloadResourcesInterval);
        }

        private void OnEnable()
        {
            Application.lowMemory += ApplicationOnLowMemory;
        }

        private void OnDisable()
        {
            Application.lowMemory -= ApplicationOnLowMemory;
            CancelInvoke(nameof(TryCleanup));
        }

        private void ApplicationOnLowMemory()
        {
            Debug.LogWarning("[MEMORY] Low memory reported. Forcing cleanup...");
            ForceCleanup();
        }

        private void TryCleanup()
        {
            // Пропускаем чистку, если локальный игрок говорит
            if (PlayerInput.Instance != null && !PlayerInput.Instance.VoiceHeld)
            {
                Debug.Log("[MEMORY] Cleanup skipped (local player is speaking).");
                return;
            }

            // Чистка разрешена, даже если другие игроки говорят
            ForceCleanup();
        }

        private void ForceCleanup()
        {
            Debug.Log("[MEMORY] Collecting GC + unloading unused assets.");
            GC.Collect();
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
    }
}