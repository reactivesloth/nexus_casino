using System.Collections.Generic;
using Code.API;
using FishNet.Object;
using Unity.Services.Vivox;
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
        
        // public VivoxParticipant Participant { get; private set; }

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
