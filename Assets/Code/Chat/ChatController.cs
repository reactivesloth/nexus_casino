using System;
using System.Linq;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using Code.Player;
using NativeWebSocket;
using TankAndHealerStudioAssets;
using UnityEngine;

namespace Code.Chat
{
    public class ChatController : MonoBehaviour
    {
        [SerializeField] private UltimateChatBox lobbyChatBox;
        [SerializeField] private UltimateChatBox globalChatBox;
        
        private PlayerMovementController PlayerMovementController => PlayerMovementController.Own;
        private WebSocket chatWebSocket;

        private UltimateChatBox _currentChatBox;
        private bool _isGlobalChatActive = false;

        private void Start()
        {
            SetCurrentChat(lobbyChatBox);
        }

        private void OnEnable()
        {
            chatWebSocket = new WebSocket("wss://wss.ru", ClientDataStorage.GetJwtHeader()); 
            //chatWebSocket.Connect();
            chatWebSocket.OnMessage += OnMessageRecived;
        }

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.Tab))
                ChangeChat();
        }

        private void OnDisable()
        {
            chatWebSocket.OnMessage -= OnMessageRecived;
            chatWebSocket.Close();
        }

        private void ChangeChat()
        {
            SetCurrentChat(_currentChatBox == lobbyChatBox ? globalChatBox : lobbyChatBox);
        }

        private void SetCurrentChat(UltimateChatBox chatBox)
        {
            if (_currentChatBox != null)
                _currentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
            
            _currentChatBox = chatBox;
            _isGlobalChatActive = _currentChatBox == globalChatBox;
            globalChatBox.gameObject.SetActive(_currentChatBox == globalChatBox);
            lobbyChatBox.gameObject.SetActive(_currentChatBox == lobbyChatBox);
            
            _currentChatBox.OnInputFieldSubmitted += OnInputFieldSubmittedCurrentBox;
        }
        
        private void OnMessageRecived(byte[] byteData)
        {
            var dataText = System.Text.Encoding.UTF8.GetString(byteData);
            var messageData = JsonUtility.FromJson<MessageData>(dataText);

            var style = UltimateChatBoxStyles.none;
            
            HandleGlobalMassage(messageData, style);
            if(messageData.lobby == LobbyVariables.Instance.currentLobby.lobbyId)
                HandleLobbyMassage(messageData, style);
        }

        private void OnInputFieldSubmittedCurrentBox(string text)
        {
            if (_currentChatBox.InputFieldContainsCommand)
                return;

            var message = new MessageData
            {
                lobby = LobbyVariables.Instance.currentLobby.lobbyId,
                text = text,
                username = ClientDataStorage.UserData.username
            };

            var style = UltimateChatBoxStyles.boldUsername;
            
            HandleLobbyMassage(message, style);
            HandleGlobalMassage(message, style);
        }
        
        private void HandleGlobalMassage(MessageData message, UltimateChatBox.ChatStyle style)
        {
            globalChatBox.RegisterChat($"[...{message.lobby.Substring(message.lobby.Length - 4)}]{message.username}", message.text, style);
        }

        private void HandleLobbyMassage(MessageData message, UltimateChatBox.ChatStyle style)
        {
            lobbyChatBox.RegisterChat($"{message.username}", message.text, style);
        }
    }
}