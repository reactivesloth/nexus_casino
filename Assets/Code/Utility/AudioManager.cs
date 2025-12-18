using UnityEngine;
using UnityEngine.Audio;

namespace Code.Utility
{
    /// <summary>
    /// Хранит громкости в линейном 0..1 и конвертирует в dB для AudioMixer.
    /// Параметры в миксере: "<Category>Volume" (например, "MusicVolume").
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioMixer audioMixer;

        private void Awake()
        {
            Instance = this;
        }

        /// <param name="category">Префикс параметра в миксере, например "Music"</param>
        /// <param name="volume01">Линейный 0..1</param>
        public void SetVolume(string category, float volume01)
        {
            if (audioMixer == null || string.IsNullOrEmpty(category)) return;

            // 0 -> -80 dB (почти mute), 1 -> 0 dB
            float v = Mathf.Clamp01(volume01);
            float dB = v <= 0.0001f ? -80f : 10f * Mathf.Log10(v);
            audioMixer.SetFloat($"{category}Volume", dB);
        }

        /// <returns>Линейный уровень 0..1 (если параметр есть), иначе 1</returns>
        public float GetVolume01(string category)
        {
            if (audioMixer != null && audioMixer.GetFloat($"{category}Volume", out float dB))
            {
                if (dB <= -80f) return 0f;
                return Mathf.Clamp01(Mathf.Pow(10f, dB / 10f));
            }
            return 1f;
        }
    }
}