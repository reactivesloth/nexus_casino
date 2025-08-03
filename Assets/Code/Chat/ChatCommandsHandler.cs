using System;
using System.Text;
using Code.API;
using Code.API.Models;
using Code.Player;
using FishNet.Component.Animating;
using FishNet.Connection;
using FishNet.Object;
using TankAndHealerStudioAssets;
using UnityEngine;

namespace Code.Chat
{
    public class ChatCommandsHandler : NetworkBehaviour
    {
        [SerializeField] private ChatController chatController;
        [SerializeField, TextArea] private string helpText;

        private void OnValidate()
        {
            chatController ??= GetComponent<ChatController>();
        }

        public void Help()
        {
            var answerMessageBuilder = new StringBuilder();

            answerMessageBuilder.AppendLine($"<color=yellow>{helpText}</color>");

            foreach (var command in chatController.CommandsDictionary.Values)
            {
                answerMessageBuilder.AppendLine(command.commandValue + " - " + command.description);
            }

            chatController.CurrentChatBox.RegisterChat(chatController.SystemName, answerMessageBuilder.ToString());
        }

        public void ShareBalance()
        {
            var user = ClientDataStorage.UserData;
            ShareBalance_ServerRpc(user.username, user.balance);
        }

        #region Share Balance RPCs

        [ServerRpc(RequireOwnership = false, RunLocally = true)]
        public void ShareBalance_ServerRpc(string nickname, int balance) =>
            ShareBalance_ObserversRpc(nickname, balance);

        [ObserversRpc(RunLocally = true)]
        public void ShareBalance_ObserversRpc(string nickname, int balance)
        {
            /*chatController.HandleLobbyMassage(new MessageData { Message = new MessageInfo{UserId = 0, Message = $"Мой баланс {balance}!"}},
                UltimateChatBoxStyles.noticeMessage);*/
        }

        #endregion

        public void Emotion(string emotion)
        {
            var animatorController = PlayerMovementController.Own.GetComponent<NetworkAnimator>();
            if(animatorController)
                animatorController.SetTrigger(emotion);
        }
    }
}