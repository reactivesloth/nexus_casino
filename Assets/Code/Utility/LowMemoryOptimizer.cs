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

        private void ApplicationOnLowMemory()
        {
            GC.Collect();
#if UNITY_EDITOR
            EditorUtility.UnloadUnusedAssetsImmediate();
#endif
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
    }
}