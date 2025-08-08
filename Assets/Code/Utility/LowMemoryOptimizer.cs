using System;
using UnityEditor;
using UnityEngine;

namespace Code.Utility
{
    public class LowMemoryOptimizer: MonoBehaviour
    {
        private void OnEnable()
        {
            Application.lowMemory += ApplicationOnLowMemory;
        }

        private void OnDisable()
        {
            Application.lowMemory -= ApplicationOnLowMemory;
        }

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