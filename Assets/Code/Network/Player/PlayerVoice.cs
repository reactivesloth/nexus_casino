using System.Collections;
using Photon.Voice.Unity;
using PurrNet;
using UnityEngine;

namespace Code.Network.Player
{
    public class PlayerVoice : NetworkBehaviour
    {
        public static PlayerVoice LocalInstance;
        public bool isMuted;
        public bool isInputMutedByServer;
        public Recorder voice;
        
        private IEnumerator Start()
        {
            yield return new WaitUntil(() => Player.GetLocalPlayer() != null);
            LocalInstance = Player.GetLocalPlayer().GetComponent<PlayerVoice>();

            voice = FindAnyObjectByType<Recorder>();
            
            SetMuteState(true);
        }

        public void SetMuteState(bool muted)
        {
            if (!isOwner) return;

            isMuted = muted || isInputMutedByServer;
            voice.TransmitEnabled = !isMuted;
        }

        private void Update()
        {
            if (!isInputMutedByServer) return;
            if (!isMuted) SetMuteState(true);
        }

        public static PlayerVoice GetByPlayerID(PlayerID playerName)
        {
            return Player.TryGetPlayer(playerName, out var player) ? player.GetComponent<PlayerVoice>() : null;
        }
    }
}