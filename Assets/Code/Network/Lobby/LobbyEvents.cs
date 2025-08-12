using Epic.OnlineServices.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Code.Network.Lobby.EOSCoroutines;
using EOSLobby;

namespace Code.Network.Lobby
{
    [DefaultExecutionOrder(-20)]
    public sealed class LobbyEvents : MonoBehaviour
    {
        [Header("General")]
        public UnityEvent<ButtonData> ButtonClicked;
        public UnityEvent<TMP_InputField> InputFieldChanged;
        public UnityEvent<Toggle> ToggleValueChanged;

        [Header("Lobby Room")]
        public UnityEvent<LobbyUpdateReceivedCallbackInfo> LobbyUpdateReceived;
        public UnityEvent<LobbyMemberStatusReceivedCallbackInfo> LobbyMemberStatusReceived;
        public UnityEvent<LobbyMemberUpdateReceivedCallbackInfo> LobbyMemberUpdateReceived;

        public static LobbyEvents Instance { get; private set; }

        private void Awake() => Instance = this;

        private void OnEnable()
        {
            // безопасно: AddNotify сами игнорят дубли
            LobbyNotify.AddNotifyLobbyUpdateReceived(LobbyUpdateReceived.Invoke);
            LobbyNotify.AddNotifyLobbyMemberStatusReceived(LobbyMemberStatusReceived.Invoke);
            LobbyNotify.AddNotifyLobbyMemberUpdateReceived(LobbyMemberUpdateReceived.Invoke);
        }

        private void OnDisable()
        {
            LobbyNotify.RemoveNotifyLobbyUpdateReceived(LobbyUpdateReceived.Invoke);
            LobbyNotify.RemoveNotifyLobbyMemberStatusReceived(LobbyMemberStatusReceived.Invoke);
            LobbyNotify.RemoveNotifyLobbyMemberUpdateReceived(LobbyMemberUpdateReceived.Invoke);
        }
    }
}