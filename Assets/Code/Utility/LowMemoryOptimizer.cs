using System;
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
            Resources.UnloadUnusedAssets();
        }
    }
}