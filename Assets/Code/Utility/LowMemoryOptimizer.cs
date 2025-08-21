using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Code.Utility
{
    public sealed class LowMemoryOptimizer : MonoBehaviour
    {
        private void OnEnable()  => Application.lowMemory += ApplicationOnLowMemory;
        private void OnDisable() => Application.lowMemory -= ApplicationOnLowMemory;

        [SerializeField] private float unloadResourcesInterval = 60;
        
        private void Awake()
        {
            if (unloadResourcesInterval > 0)
                Invoke(nameof(ApplicationOnLowMemory), unloadResourcesInterval);
        }

        private void ApplicationOnLowMemory()
        {
            Debug.Log("[MEMORY] Low Memory Optimizer collect GC and unload ol unused resources.");
            GC.Collect();
#if UNITY_EDITOR
            EditorUtility.UnloadUnusedAssetsImmediate();
#endif
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
    }
}