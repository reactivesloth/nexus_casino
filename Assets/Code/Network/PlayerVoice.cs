using System.Collections.Generic;
using Code.API;
using Code.UI;
using Code.Utility;
using CrazyMinnow.SALSA;
using FishNet.Object;
using Unity.Services.Vivox;
using UnityEngine;

namespace Code.Network
{
    public class PlayerVoice : NetworkBehaviour
    {
        #region Singleton
        public static PlayerVoice LocalPlayerVoiceInstance { get; private set; }
        private readonly static List<PlayerVoice> instances = new();
        public static IReadOnlyList<PlayerVoice> Instances => instances;
        #endregion
        
        public bool isInputMuted;
        public bool isInputMutedByServer;
        
        private float savedVol;
        private float savedVolSettings;
        private VivoxParticipant participant;
        private Salsa salsa;

        private void Awake()
        {
            salsa = gameObject.GetComponentInChildren<Salsa>();
            if (salsa != null)
                salsa.useExternalAnalysis = true;
        }

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = this;
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    VivoxVoiceManager.Instance.LoginToVivox();
                    InvokeRepeating(nameof(UpdatePos), 0, 0.1f);
                    savedVolSettings = 0;
                    isInputMuted = true;
                    VivoxVoiceManager.Instance.MuteLocalPlayer();
                }
                else
                {
                    VivoxVoiceManager.Instance.LogoutOfVivoxServiceAsync(true);
                    CancelInvoke(nameof(UpdatePos));
                }
            }

            instances.Add(this);
         }
        
        public override void OnStopClient()
        {
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = null;
                VivoxVoiceManager.Instance.LogoutOfVivoxServiceAsync();
            }

            instances.Remove(this);

            participant = null;
        }

        private void OnDestroy()
        {
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = null;
                VivoxVoiceManager.Instance.LogoutOfVivoxServiceAsync();
            }

            instances.Remove(this);

            participant = null;
        }

        private void Update()
        {
            if (isInputMutedByServer)
                isInputMuted = true;

            if (participant == null)
            {
                participant = VivoxVoiceManager.Instance.GetParticipant(gameObject.GetComponentInChildren<PlayerUI>().PlayerName);
            }
            else
            {
                if (salsa != null)
                {
                    if (participant != null)
                    {
                        if (participant.SpeechDetected)
                        {
                            var audioEnergy = participant.AudioEnergy;
                            if (participant.IsMuted) audioEnergy = 0f;
                            if (audioEnergy < 0.01f) audioEnergy = 0f;
                            salsa.analysisValue = (float)audioEnergy;
                        }
                        else
                        {
                            salsa.analysisValue = 0;
                        }
                    }
                }

                if (!IsOwner) return;

                if (isInputMuted && !participant.IsMuted)
                {
                    VivoxVoiceManager.Instance.MuteLocalPlayer();
                }
                else if (!isInputMuted && participant.IsMuted)
                {
                    VivoxVoiceManager.Instance.UnmuteLocalPlayer();
                }

                if (!Mathf.Approximately(savedVolSettings, SettingsManager.Instance.VoiceChatVolume))
                {
                    VivoxService.Instance.SetOutputDeviceVolume((int)Mathf.Lerp(-40, 10, SettingsManager.Instance.VoiceChatVolume / 100));
                    savedVolSettings = SettingsManager.Instance.VoiceChatVolume;
                }
            }
        }

        private void UpdatePos()
        {
            if (VivoxService.Instance?.ActiveChannels?.Count > 0)
                VivoxVoiceManager.Instance.SetLocalPosition(gameObject);
        }

    }
}
