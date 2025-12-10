using System.Collections.Generic;
using FishNet.Object;
// using Unity.Services.Vivox;
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
        
        //public VivoxParticipant Participant { get; private set; }

        public override void OnStartClient()
        {
            #region Singleton
            if (IsOwner)
            {
                LocalPlayerVoiceInstance = this;
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
            }

            instances.Remove(this);
            #endregion
            
            PlayFlowFishnet flowFishnet = FindAnyObjectByType<PlayFlowFishnet>(FindObjectsInactive.Include);
            flowFishnet.LogoutOfVivoxServiceAsync();
        }

        private void Update()
        {
            if (isInputMutedByServer)
                isInputMuted = true;
            
            // if (Participant == null) return;
            //
            // if (isInputMuted != Participant.IsMuted)
            // {
            //     if (isInputMuted)
            //         Participant.MutePlayerLocally();
            //     else
            //         Participant.UnmutePlayerLocally();
            // }
        }
    }
}
