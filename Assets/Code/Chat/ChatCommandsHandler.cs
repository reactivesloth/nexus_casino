using System.Text;
using Code.API;
using Code.Player;
using FishNet.Component.Animating;
using FishNet.Object;
using UnityEngine;

namespace Code.Chat
{
    public class ChatCommandsHandler : NetworkBehaviour
    {
        [SerializeField] private ChatController chatController;
        [SerializeField, TextArea] private string helpText;

        private void OnValidate()
        {
            if (chatController == null)
                chatController = GetComponent<ChatController>();
        }

        public void Help()
        {
            if (chatController == null || chatController.CurrentChatBox == null) return;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(helpText))
                sb.AppendLine($"<color=yellow>{helpText}</color>");

            // без LINQ/foreach alloc
            var dict = chatController.CommandsDictionary;
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    var cmd = kv.Value;
                    if (cmd != null)
                        sb.AppendLine(cmd.commandValue + " - " + cmd.description);
                }
            }

            chatController.CurrentChatBox.RegisterChat(chatController.SystemName, sb.ToString());
        }

        public void ShareBalance()
        {
            var user = ClientDataStorage.UserData;
            if (user == null) return;
            ShareBalance_ServerRpc(user.username, user.balance);
        }

        [ServerRpc(RequireOwnership = false, RunLocally = true)]
        private void ShareBalance_ServerRpc(string nickname, int balance)
        {
            ShareBalance_ObserversRpc(nickname, balance);
        }

        [ObserversRpc(RunLocally = true)]
        private void ShareBalance_ObserversRpc(string nickname, int balance)
        {
            // пример системного сообщения
            // chatController?.SendSystemMessage($"{nickname}: мой баланс {balance}", UltimateChatBoxStyles.noticeMessage);
        }

        public void Emotion(string emotion)
        {
            if (string.IsNullOrEmpty(emotion) || PlayerMovementController.Own == null) return;
            if (PlayerMovementController.Own.TryGetComponent(out NetworkAnimator na))
                na.SetTrigger(emotion);
        }
    }
}
