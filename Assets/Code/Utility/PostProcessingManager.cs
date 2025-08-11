using UnityEngine;
using UnityEngine.Rendering;

namespace Code.Utility
{
    public sealed class PostProcessingManager : MonoBehaviour
    {
        public static PostProcessingManager Instance { get; private set; }

        [SerializeField] private Volume postProcessVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetEnabled(bool enabled)
        {
            if (postProcessVolume != null)
                postProcessVolume.enabled = enabled;
        }

        public bool IsEnabled() => postProcessVolume != null && postProcessVolume.enabled;
    }
}