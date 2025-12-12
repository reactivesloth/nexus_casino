using System.Collections.Generic;
using Code.API;
using Code.UI;
using Code.Utility;
using CrazyMinnow.SALSA;
using FishNet.Object;
using Unity.Services.Vivox;
using UnityEngine;
#if AUTH_PACKAGE_PRESENT
using Unity.Services.Authentication;
#endif

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
                    LoginToVivox();
                else
                    LogoutOfVivoxServiceAsync(true);
            }

            instances.Add(this);
         }
        
        public override void OnStopClient()
        {
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = null;
                LogoutOfVivoxServiceAsync();
            }

            instances.Remove(this);

            participant = null;
        }

        private void OnDestroy()
        {
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = null;
                LogoutOfVivoxServiceAsync();
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
                    var audioEnergy = participant.AudioEnergy;
                    if (participant.IsMuted) audioEnergy = 0f;
                    if (audioEnergy < 0.01f) audioEnergy = 0f;
                    salsa.analysisValue = (float)audioEnergy;
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

                if (savedVolSettings != SettingsManager.Instance.VoiceChatVolume)
                {
                    VivoxService.Instance.SetOutputDeviceVolume((int)(Mathf.Lerp(-40, 10,
                        SettingsManager.Instance.VoiceChatVolume / 100)));
                    savedVolSettings = SettingsManager.Instance.VoiceChatVolume;
                }
            }
        }

        private async void LoginToVivox()
        {
            var correctedDisplayName = ClientDataStorage.UserData.username;
                
            await VivoxVoiceManager.Instance.InitializeAsync(correctedDisplayName);
            var loginOptions = new LoginOptions()
            {
                DisplayName = correctedDisplayName,
                ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
            };
            await VivoxService.Instance.LoginAsync(loginOptions);
            VivoxVoiceManager.Instance.ConnectToLobbyChannel();
            InvokeRepeating(nameof(UpdatePos), 0, 0.1f);
            savedVolSettings = 0;
            isInputMuted = true;
            VivoxVoiceManager.Instance.MuteLocalPlayer();

            VivoxService.Instance.VivoxGlobalAudioSettings.PlatformAcousticEchoCancellationEnabled = false;
            VivoxService.Instance.VivoxGlobalAudioSettings.AudioClippingProtectorEnabled = true;
            VivoxService.Instance.VivoxGlobalAudioSettings.VivoxAcousticEchoCancellationEnabled = true;
            VivoxService.Instance.VivoxGlobalAudioSettings.AutomaticGainControlEnabled = true;
            VivoxService.Instance.VivoxGlobalAudioSettings.NoiseSuppressionEnabled = true;
            
            VivoxService.Instance.EnableAcousticEchoCancellation();
        }

        private void LogoutOfVivoxServiceAsync(bool rejoinAfter = false)
        {
            VivoxService.Instance.LogoutAsync();
#if AUTH_PACKAGE_PRESENT
        AuthenticationService.Instance.SignOut();
#endif
            VivoxVoiceManager.Instance.DisconnectFromLobbyChannel();
            CancelInvoke(nameof(UpdatePos));

            if (rejoinAfter)
            {
                LoginToVivox();
            }
        }

        private void UpdatePos()
        {
            VivoxVoiceManager.Instance.SetLocalPosition(gameObject);
        }

    }
}
