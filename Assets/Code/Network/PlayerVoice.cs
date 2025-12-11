using System.Collections.Generic;
using Code.API;
using Code.Utility;
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
        
        public override void OnStartClient()
        {
            #region Singleton
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = this;
                LoginToVivox();
            }

            instances.Add(this);
            #endregion

            isInputMuted = true;
        }

        public override void OnStopClient()
        {
            #region Singleton
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = null;
                LogoutOfVivoxServiceAsync();
            }

            instances.Remove(this);
            #endregion
        }

        private void Update()
        {
            if (isInputMutedByServer)
                isInputMuted = true;

            if (IsOwner)
            {
                if (isInputMuted)
                {
                    VivoxVoiceManager.Instance.MuteLocalPlayer();
                }
                else
                {
                    VivoxVoiceManager.Instance.UnmuteLocalPlayer();
                }
            }

            if (savedVolSettings != SettingsManager.Instance.VoiceChatVolume)
            {
                VivoxService.Instance.SetOutputDeviceVolume((int)(Mathf.Lerp(-40, 10, SettingsManager.Instance.VoiceChatVolume / 100)));
                savedVolSettings = SettingsManager.Instance.VoiceChatVolume;
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
        }
        
        private async void LogoutOfVivoxServiceAsync()
        {
            await VivoxService.Instance.LogoutAsync();
#if AUTH_PACKAGE_PRESENT
        AuthenticationService.Instance.SignOut();
#endif
            VivoxVoiceManager.Instance.DisconnectFromLobbyChannel();
            CancelInvoke(nameof(UpdatePos));
        }

        private void UpdatePos()
        {
            VivoxVoiceManager.Instance.SetLocalPosition(gameObject);
        }

    }
}
