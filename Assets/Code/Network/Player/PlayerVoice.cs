using System.Collections;
using CrazyMinnow.SALSA;
using UnityEngine;

namespace Code.Network.Player
{
    public class PlayerVoice : MonoBehaviour
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
    }
}