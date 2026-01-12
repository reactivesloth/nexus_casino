using System;
using UnityEngine;

namespace Code.Network.InteractionSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioInteractable : Interactable
    {
        [Header("Audio Settings")] [SerializeField]
        private AudioSource audioSource;

        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();

        [SerializeField, Tooltip("Громкость воспроизведения [0..1].")]
        private float volume = 1f;

        [SerializeField, Tooltip("Минимальная задержка между повторами, сек.")]
        private float minInterval = 0.1f;

        [SerializeField, Tooltip("Если клипов несколько — включить случайный выбор.")]
        private bool randomize = true;

        [SerializeField, Tooltip("Запретить наложение: не запускать, если клип уже играет.")]
        private bool preventOverlap = true;

        [SerializeField, Tooltip("Разрешить повтор одного и того же клипа подряд, если randomize включен.")]
        private bool allowImmediateRepeat = false;

        [SerializeField, Tooltip("Если true — при завершении интеракции остановит звук.")]
        private bool stopOnEndInteract = false;

        [Header("3D Settings")] [SerializeField, Tooltip("Включить 3D режим источника.")]
        private bool spatialize = true;

        [SerializeField, Range(0f, 1f), Tooltip("0 — 2D, 1 — полностью 3D.")]
        private float spatialBlend = 1f;

        [SerializeField, Tooltip("Минимальная дистанция для 3D затухания.")]
        private float minDistance = 1f;

        [SerializeField, Tooltip("Максимальная дистанция для 3D затухания.")]
        private float maxDistance = 20f;

        private float lastPlayTime = -999f;
        private int lastClipIndex = -1;

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
            Configure3DDefaults();
        }

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            Configure3DDefaults();
        }

        private void Configure3DDefaults()
        {
            if (audioSource == null) return;

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialize = spatialize;
            audioSource.spatialBlend = spatialBlend;
            audioSource.minDistance = Mathf.Max(0.01f, minDistance);
            audioSource.maxDistance = Mathf.Max(audioSource.minDistance + 0.01f, maxDistance);
            audioSource.volume = Mathf.Clamp01(volume);
        }

        protected override void OnInteractCallback_Client(bool success, bool force = false)
        {
            base.OnInteractCallback_Client(success, force);
            
            if(!success)
            {
                // none sucsess action
                return;
            }
            
            TryPlay();
        }

        protected override void OnInteractEndCallback_Client(bool success)
        {
            base.OnInteractEndCallback_Client(success);
            if (!success) return;

            if (stopOnEndInteract && audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (clips == null)
                clips = Array.Empty<AudioClip>();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            Configure3DDefaults();
        }
#endif
        
        private void TryPlay()
        {
            if (audioSource == null) return;
            if (clips == null || clips.Length == 0) return;

            if (Time.unscaledTime - lastPlayTime < Mathf.Max(0f, minInterval))
                return;

            if (preventOverlap && audioSource.isPlaying)
                return;

            var clip = PickClip();
            if (clip == null) return;

            audioSource.volume = Mathf.Clamp01(volume);
            audioSource.clip = clip;
            audioSource.Play();

            lastPlayTime = Time.unscaledTime;
            Invoke("RequestEndInteract", clip.length);
        }
        
        private AudioClip PickClip()
        {
            if (clips.Length == 1)
            {
                lastClipIndex = 0;
                return clips[0];
            }

            if (!randomize)
            {
                lastClipIndex = (lastClipIndex + 1 + clips.Length) % clips.Length;
                return clips[lastClipIndex];
            }

            int idx;
            if (allowImmediateRepeat || lastClipIndex < 0 || clips.Length <= 1)
            {
                idx = UnityEngine.Random.Range(0, clips.Length);
            }
            else
            {
                idx = UnityEngine.Random.Range(0, clips.Length);
                const int maxAttempts = 4;
                int attempts = 0;
                while (idx == lastClipIndex && attempts < maxAttempts)
                {
                    idx = UnityEngine.Random.Range(0, clips.Length);
                    attempts++;
                }
            }

            lastClipIndex = idx;
            return clips[idx];
        }
    }
}