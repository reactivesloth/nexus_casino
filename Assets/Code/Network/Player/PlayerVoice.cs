using System.Collections;
using Code.API;
using Code.Utility;
using CrazyMinnow.SALSA;
using PurrNet;
using Unity.Services.Vivox;
using UnityEngine;

namespace Code.Network.Player
{
    public class PlayerVoice : NetworkBehaviour
    {
        public static PlayerVoice LocalInstance;
        public bool isMuted;
        public bool isInputMutedByServer;

        private Salsa _salsa;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => Player.GetLocalPlayer() != null);
            LocalInstance = Player.GetLocalPlayer().GetComponent<PlayerVoice>();
            _salsa = Player.GetLocalPlayer().GetComponent<Salsa>();

            isMuted = true;
        }
        
        private float _savedVol;
        private float _savedVolSettings;
        private VivoxParticipant _participant;
        
        private void Awake()
        {
            _salsa = gameObject.GetComponentInChildren<Salsa>();
            if (_salsa != null)
                _salsa.useExternalAnalysis = true;
        }

        protected override async void OnSpawned ()
        {
            base.OnSpawned();
            if (isOwner)
            {
                string userName = ClientDataStorage.UserData.username;

                if (VivoxVoiceManager.Instance != null)
                {
                    if (!VivoxService.Instance.IsLoggedIn)
                    {
                        await VivoxVoiceManager.Instance.LoginToVivoxAsync(userName);
                    }

                    VivoxVoiceManager.Instance.ConnectToLobbyChannel();
                }

                InvokeRepeating(nameof(UpdatePos), 1.0f, 0.1f);
                
                ApplyAudioSettings();
            }
        }

        protected override void OnDespawned ()
        {
            base.OnDespawned();
            if (isOwner)
            {
                LeaveVoiceChannel();
            }

            _participant = null;
        }

        protected override void OnDestroy()
        {
            if (isOwner)
            {
                LeaveVoiceChannel();
            }
        }

        private void LeaveVoiceChannel()
        {
            CancelInvoke(nameof(UpdatePos));
            if (VivoxVoiceManager.Instance != null) VivoxVoiceManager.Instance.DisconnectFromLobbyChannel();
        }

        private void ApplyAudioSettings()
        {
            _savedVolSettings = 0;
            isMuted = true;
            if (VivoxVoiceManager.Instance != null) VivoxVoiceManager.Instance.MuteLocalPlayer();
        }

        public void SetMuteState(bool muted)
        {
            if (!isOwner) return;

            if (isInputMutedByServer)
                isMuted = true;

            isMuted = muted;

            if (VivoxVoiceManager.Instance != null)
            {
                if (muted)
                {
                    VivoxVoiceManager.Instance.MuteLocalPlayer();
                }
                else
                {
                    VivoxVoiceManager.Instance.UnmuteLocalPlayer();
                }
            }
        }

        private void Update()
        {
            if (isInputMutedByServer)
                isMuted = true;

            if (_participant == null)
            {
                if (VivoxVoiceManager.Instance != null)
                    _participant =
                        VivoxVoiceManager.Instance.GetParticipant(gameObject.GetComponentInChildren<PlayerUI>()
                            .PlayerName);
            }
            else
            {
                if (_salsa != null)
                {
                    if (_participant != null)
                    {
                        var audioEnergy = _participant.AudioEnergy;
                        if (_participant.IsMuted) audioEnergy = 0f;
                        if (audioEnergy < 0.01f) audioEnergy = 0f;
                        _salsa.analysisValue = (float)audioEnergy;
                    }
                }

                if (!isOwner) return;
                if (!Mathf.Approximately(_savedVolSettings, SettingsManager.Instance.VoiceChatVolume))
                {
                    if (VivoxService.Instance != null)
                        VivoxService.Instance.SetOutputDeviceVolume((int)Mathf.Lerp(-40, 10,
                            SettingsManager.Instance.VoiceChatVolume / 100));
                    _savedVolSettings = SettingsManager.Instance.VoiceChatVolume;
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