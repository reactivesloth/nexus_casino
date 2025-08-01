using UnityEngine;
using UnityEngine.Audio;

namespace Code.Utility
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioMixer audioMixer;

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

        public void SetVolume(string category, float volume)
        {
            float dbVolume = Mathf.Log10(Mathf.Clamp(volume, 0.001f, 1f)) * 20f;
            audioMixer.SetFloat($"{category}Volume", dbVolume * 100);
        }

        public float GetVolume(string category)
        {
            if (audioMixer.GetFloat($"{category}Volume", out float db))
                return Mathf.Pow(10f, db / 20f)/100;
            return 1f;
        }
    }
}