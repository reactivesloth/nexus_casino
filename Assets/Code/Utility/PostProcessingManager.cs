using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Code.Utility
{
    public class PostProcessingManager : MonoBehaviour
    {
        public static PostProcessingManager Instance { get; private set; }

        [SerializeField] private Volume postProcessVolume;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetEnabled(bool enabled)
        {
            if (postProcessVolume != null)
                postProcessVolume.enabled = enabled;
        }

        public bool IsEnabled() => postProcessVolume != null && postProcessVolume.enabled;
    }
}