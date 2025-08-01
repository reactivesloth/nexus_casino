using System;
using System.Collections.Generic;
using System.Linq;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using Code.Player;
using Code.Utility;
using NativeWebSocket;
using Proyecto26;
using TankAndHealerStudioAssets;
using UnityEngine;
using UnityEngine.Events;

namespace Code.Chat
{
    public class ChatController : MonoBehaviour
    {
        [SerializeField] private string systemName = "[SYSTEM]";
        [SerializeField] private UltimateChatBox lobbyChatBox;
        [SerializeField] private UltimateChatBox globalChatBox;
        [SerializeField] private List<CommandData> commands;

        public readonly Dictionary<string, CommandData> CommandsDictionary = new();
        private PlayerMovementController PlayerMovementController => PlayerMovementController.Own;
        private WebSocket chatWebSocket;

        public UltimateChatBox CurrentChatBox { get; private set; }
        private bool _isGlobalChatActive = false;

        public string SystemName => systemName;

        private void Awake()
        {
            commands.ForEach(c => CommandsDictionary.Add(c.commandValue, c));
        }

        private void Start()
        {
            SetCurrentChat(lobbyChatBox);

            CurrentChatBox.DisableInputField();
            CurrentChatBox.Disable();
        }

        private void OnEnable()
        {
            chatWebSocket = new WebSocket("wss://back.nexusmetaclub.com/api/client/ws/lobby",
                ClientDataStorage.GetJwtHeader());
            chatWebSocket.Connect();
            chatWebSocket.OnMessage += OnMessageRecived;
        }

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.Tab) && CurrentChatBox.IsEnabled)
                ChangeChat();

            if (chatWebSocket.State == WebSocketState.Open)
                PingChatConnection();
        }

        private float _pingInterval = 2f;
        private float _currentPingInterval = 0;
        private void PingChatConnection()
        {
            _currentPingInterval += Time.deltaTime;
            if (_currentPingInterval >= _pingInterval)
            {
                _currentPingInterval = 0;
                chatWebSocket.SendText("{\n    \"event\": \"ping\",\n    \"data\": {}\n}");
            }
        }

        private void OnDisable()
        {
            chatWebSocket.OnMessage -= OnMessageRecived;
            chatWebSocket.Close();
        }

        private void ChangeChat()
        {
            SetCurrentChat(CurrentChatBox == lobbyChatBox ? globalChatBox : lobbyChatBox);
        }

        private void SetCurrentChat(UltimateChatBox chatBox)
        {
            if (CurrentChatBox != null)
            {
                CurrentChatBox.OnInputFieldEnabled -= OnInputFieldEnabled;
                CurrentChatBox.OnInputFieldDisabled -= OnInputFieldDisabled;
                CurrentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
                CurrentChatBox.OnInputFieldCommandSubmitted -= ChatBoxOnOnInputFieldCommandSubmitted;

                CurrentChatBox.Disable();
            }

            CurrentChatBox = chatBox;
            _isGlobalChatActive = CurrentChatBox == globalChatBox;
            globalChatBox.gameObject.SetActive(CurrentChatBox == globalChatBox);
            lobbyChatBox.gameObject.SetActive(CurrentChatBox == lobbyChatBox);

            CurrentChatBox.OnInputFieldEnabled += OnInputFieldEnabled;
            CurrentChatBox.OnInputFieldDisabled += OnInputFieldDisabled;
            CurrentChatBox.OnInputFieldSubmitted += OnInputFieldSubmittedCurrentBox;
            CurrentChatBox.OnInputFieldCommandSubmitted += ChatBoxOnOnInputFieldCommandSubmitted;

            CurrentChatBox.EnableInputField();
            CurrentChatBox.Enable();
        }

        private void OnInputFieldEnabled()
        {
            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.ShowCursor();
            }
        }

        private void OnInputFieldDisabled()
        {
            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.HideCursor();
            }
        }

        private void ChatBoxOnOnInputFieldCommandSubmitted(string command, string message)
        {
            if (!CommandsDictionary.TryGetValue(command, out var commandData))
            {
                CurrentChatBox.RegisterChat(systemName, "command not found", UltimateChatBoxStyles.errorMessage);
                return;
            }

            if (commandData.requireMessageValue && string.IsNullOrEmpty(message))
            {
                CurrentChatBox.RegisterChat(systemName, "command need value", UltimateChatBoxStyles.errorMessage);
                return;
            }

            commandData.unityEvent?.Invoke(message);
        }

        private void OnMessageRecived(byte[] byteData)
        {
            var dataText = System.Text.Encoding.UTF8.GetString(byteData);
            var messageData = JsonUtility.FromJson<MessageData>(dataText);

            Debug.Log(messageData);


            HandleGlobalMassage(messageData);
            if (messageData.Message.LobbyId == LobbyVariables.Instance.currentLobby.lobbyId)
                HandleLobbyMassage(messageData);
        }

        private void OnInputFieldSubmittedCurrentBox(string text)
        {
            if (CurrentChatBox.InputFieldContainsCommand)
                return;

            var message = new MessageData
            {
                Message = new MessageInfo
                {
                    LobbyId = LobbyVariables.Instance.currentLobby.lobbyId,
                    Message = text,
                    UserId = 0
                    // username = ClientDataStorage.UserData.username
                }
            };

            var style = UltimateChatBoxStyles.boldUsername;

            HandleLobbyMassage(message, style);
            HandleGlobalMassage(message, style);
            
            //TODO: Send

            var sendRequest = new RequestHelper
            {
                Uri = ApiRoutes.SendMessageUrl(),
                Headers = ClientDataStorage.GetJwtHeader(),
                Body = new SendMessageRequest
                {
                    lobby_id = LobbyVariables.Instance.currentLobby.lobbyId,
                    message = text,
                    type = "message"
                },
            };

            RestClient.Post(sendRequest);
        }

        public void HandleGlobalMassage(MessageData message, UltimateChatBox.ChatStyle style = null)
        {
            globalChatBox.RegisterChat($"[...{message.Message.LobbyId.Substring(message.Message.LobbyId.Length - 4)}]{message.Message.UserId}",
                message.Message.Message, style);
        }

        public void HandleLobbyMassage(MessageData message, UltimateChatBox.ChatStyle style = null)
        {
            lobbyChatBox.RegisterChat($"{message.Message.UserId}", message.Message.Message, style);
        }
    }

    [Serializable]
    public class CommandData
    {
        public string commandValue;
        public bool requireMessageValue = false;
        public UnityEvent<string> unityEvent;

        [TextArea] public string description;
    }
}