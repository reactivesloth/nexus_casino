using System;
using Dissonance;
using UnityEngine;

namespace Code.Player
{
    public class VoiceChatInputHandler : MonoBehaviour
    {
        [SerializeField] private VoiceBroadcastTrigger voiceBroadcastTrigger;

        private void OnValidate()
        {
            voiceBroadcastTrigger ??= GetComponent<VoiceBroadcastTrigger>();
        }

        private void Update()
        {
            voiceBroadcastTrigger.VoiceHeld = PlayerInput.Instance.VoiceHeld;
        }
    }
}
