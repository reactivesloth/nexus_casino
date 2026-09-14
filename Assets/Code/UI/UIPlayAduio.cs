using System.Collections.Generic;
using UnityEngine;

namespace Code.UI
{
    public class UIPlayAudio : MonoBehaviour
    {
        public List<AudioClip> UISounds = new List<AudioClip>();
        public static UIPlayAudio Instance;

        private void Awake()
        {
            Instance = this;
        }

        public void PlayAudio(int index)
        {
            var audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource && UISounds.Count > index) audioSource.clip = UISounds[index]; audioSource.Play();
        }
    }
}