using System.Collections.Generic;
using Code.API;
using Code.UI;
using Code.Utility;
using CrazyMinnow.SALSA;
using PurrNet;
using PurrNet.Transports;
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

        public async void OnConnectedToServer ()
        {
            if (isOwner)
            {
                LocalPlayerVoiceInstance = this;

                string userName = ClientDataStorage.UserData.username;
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxVoiceManager.Instance.LoginToVivoxAsync(userName);
                }
                networkManager.onClientConnectionState += NetworkManagerOnonClientConnectionState;
            }

            instances.Add(this);
        }

        private void NetworkManagerOnonClientConnectionState(ConnectionState obj)
        {
            if (obj == ConnectionState.Connected)
            {
                VivoxVoiceManager.Instance.ConnectToLobbyChannel();

                InvokeRepeating(nameof(UpdatePos), 1.0f, 0.1f);
                ApplyAudioSettings();

            }

            if (obj == ConnectionState.Disconnected)
            {
                {
                     LocalPlayerVoiceInstance = null;
                     LeaveVoiceChannel();
                }
                
                instances.Remove(this);
                participant = null;
            }
        }

        protected override void OnDestroy()
        {
            if (isOwner && LocalPlayerVoiceInstance == this)
            {
                LocalPlayerVoiceInstance = null;
                LeaveVoiceChannel();
            }

            instances.Remove(this);
        }

        private void LeaveVoiceChannel()
        {
            CancelInvoke(nameof(UpdatePos));

            VivoxVoiceManager.Instance.DisconnectFromLobbyChannel();
        }

        private void ApplyAudioSettings()
        {
            savedVolSettings = 0;
            isInputMuted = true;
            VivoxVoiceManager.Instance.MuteLocalPlayer();
        }
        
        public void SetMuteState(bool muted)
        {
            if (!isOwner) return;
         
            if (isInputMutedByServer)
                isInputMuted = true;
            
            isInputMuted = muted;
            
            if (muted)
                VivoxVoiceManager.Instance.MuteLocalPlayer();
            else
                VivoxVoiceManager.Instance.UnmuteLocalPlayer();
        }

        private void Update()
        {
            if (isInputMutedByServer)
                isInputMuted = true;

            if (participant == null)
            {
                participant =
                    VivoxVoiceManager.Instance.GetParticipant(gameObject.GetComponentInChildren<PlayerUI>().PlayerName);
            }
            else
            {
                if (salsa != null)
                {
                    if (participant != null)
                    {
                        var audioEnergy = participant.AudioEnergy;
                        if (participant.IsMuted) audioEnergy = 0f;
                        if (audioEnergy < 0.01f) audioEnergy = 0f;
                        salsa.analysisValue = (float)audioEnergy;
                    }
                }

                if (!isOwner) return;
                if (!Mathf.Approximately(savedVolSettings, SettingsManager.Instance.VoiceChatVolume))
                {
                    VivoxService.Instance.SetOutputDeviceVolume((int)Mathf.Lerp(-40, 10,
                        SettingsManager.Instance.VoiceChatVolume / 100));
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