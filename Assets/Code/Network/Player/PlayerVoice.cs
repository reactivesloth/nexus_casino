using System.Collections;
using Code.Utility;
using CrazyMinnow.SALSA;
using PurrNet;
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
        
        private void Awake()
        {
            _salsa = gameObject.GetComponentInChildren<Salsa>();
            if (_salsa != null)
                _salsa.useExternalAnalysis = true;
        }

        protected override void OnSpawned ()
        {
            base.OnSpawned();
            ApplyAudioSettings();
        }
        
        protected override void OnDespawned ()
        {
            base.OnDespawned();
            if (isOwner)
            {
                ConnectVoiceChannel();
                LeaveVoiceChannel();
            }
        }

        protected override void OnDestroy()
        {
            if (isOwner)
            {
                LeaveVoiceChannel();
            }
        }

        private void ConnectVoiceChannel()
        {
            
        }

        private void LeaveVoiceChannel()
        {
            
        }

        private void ApplyAudioSettings()
        {
            _savedVolSettings = 0;
            isMuted = true;
        }

        public void SetMuteState(bool muted)
        {
            if (!isOwner) return;

            if (isInputMutedByServer)
                isMuted = true;

            isMuted = muted;
        }

        private void Update()
        {
            if (isInputMutedByServer)
                isMuted = true;
            
            if (_salsa != null)
            { 
                _salsa.analysisValue = 0;
            }

            if (!isOwner) return;
            if (!Mathf.Approximately(_savedVolSettings, SettingsManager.Instance.VoiceChatVolume))
            {
                _savedVolSettings = SettingsManager.Instance.VoiceChatVolume;
            }
        }

        public static PlayerVoice GetByPlayerID(PlayerID playerName)
        {
            return Player.TryGetPlayer(playerName, out var player) ? player.GetComponent<PlayerVoice>() : null;
        }
    }
}