using System;
using System.Collections.Generic;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using Code.Player;
using NativeWebSocket;
using Proyecto26;
using TankAndHealerStudioAssets;
using UnityEngine;

namespace Code.Chat
{
    public class ChatController : MonoBehaviour
    {
        [SerializeField] private string systemName = "[SYSTEM]";
        [SerializeField] private UltimateChatBox lobbyChatBox;
        [SerializeField] private UltimateChatBox globalChatBox;
        [SerializeField] private List<CommandData> commands;

        [Header("ChatPosition settings")] [SerializeField]
        private Vector2 desktopPosition;

        [SerializeField] private Vector2 mobilePosition;

        public readonly Dictionary<string, CommandData> CommandsDictionary = new();
        private PlayerMovementController PlayerMovementController => PlayerMovementController.Own;
        private WebSocket chatWebSocket;

        public UltimateChatBox CurrentChatBox { get; private set; }
        private bool _isGlobalChatActive = false;

        public string SystemName => systemName;

        private void Start()
        {
            commands.ForEach(c => CommandsDictionary.Add(c.commandValue, c));

            lobbyChatBox.chatBoxPosition =
                PlayerInput.Instance.IsUsingMobileFallback ? mobilePosition : desktopPosition;
            lobbyChatBox.UpdatePositioning();
            globalChatBox.chatBoxPosition =
                PlayerInput.Instance.IsUsingMobileFallback ? mobilePosition : desktopPosition;
            globalChatBox.UpdatePositioning();
        }

        private void OnEnable()
        {
            SetCurrentChat(lobbyChatBox);
            CurrentChatBox.Disable();
            CurrentChatBox.DisableInputField();

            chatWebSocket =
                new WebSocket(
                    $"ws://back.nexusmetaclub.com/api/client/ws/lobby?jwt={ClientDataStorage.AccessToken}&lobby_id={"main"}");

            chatWebSocket.OnOpen += ChatWebSocketOnOnOpen;
            chatWebSocket.OnMessage += OnMessageRecived;
            chatWebSocket.OnError += ChatWebSocketOnOnError;
            chatWebSocket.OnClose += ChatWebSocketOnOnClose;

            chatWebSocket.Connect();

            PlayerInput.Instance.SwitchChatButton.gameObject.SetActive(false);
        }

        private void ChatWebSocketOnOnOpen()
        {
            SendSystemMessage(
                $"Chat connection open.",
                UltimateChatBoxStyles.noticeMessage);
        }

        private void ChatWebSocketOnOnClose(WebSocketCloseCode closeCode)
        {
            SendSystemMessage(
                $"Chat connection close, close code is {(int)closeCode} {closeCode.ToString()}",
                UltimateChatBoxStyles.noticeMessage);

            if (closeCode != WebSocketCloseCode.Normal)
                chatWebSocket.Connect();
        }

        private void ChatWebSocketOnOnError(string errorMsg)
        {
            SendSystemMessage(errorMsg, UltimateChatBoxStyles.errorMessage);
        }

        private void Update()
        {
            var input = PlayerInput.Instance;
            if (input.IsOpenChatDown)
                OpenChat();

            if (input.IsSwitchChatDown && CurrentChatBox.IsEnabled)
                ChangeChat();

            if (PlayerInput.Instance.IsPausedDown)
                CurrentChatBox.Disable();

#if !UNITY_WEBGL || UNITY_EDITOR
            chatWebSocket.DispatchMessageQueue();
#endif

            if (chatWebSocket.State == WebSocketState.Open)
                PingChatConnection();
        }

        private float _pingInterval = 5f;
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
            chatWebSocket.OnOpen -= ChatWebSocketOnOnOpen;
            chatWebSocket.OnMessage -= OnMessageRecived;
            chatWebSocket.OnError -= ChatWebSocketOnOnError;
            chatWebSocket.OnClose -= ChatWebSocketOnOnClose;;
            chatWebSocket.Close();
        }

        private void OpenChat()
        {
            var open = !CurrentChatBox.IsEnabled;

            if (open)
            {
                CurrentChatBox.Enable();
                CurrentChatBox.EnableInputField();
            }
            else
            {
                CurrentChatBox.DisableInputField();
                CurrentChatBox.Disable();
            }
        }

        private void ChangeChat()
        {
            SetCurrentChat(CurrentChatBox == lobbyChatBox ? globalChatBox : lobbyChatBox);
        }

        private void SetCurrentChat(UltimateChatBox chatBox)
        {
            if (CurrentChatBox != null)
            {
                CurrentChatBox.OnExtraImageInteract -= SendMessage;
                CurrentChatBox.OnInputFieldEnabled -= OnInputFieldEnabled;
                CurrentChatBox.OnInputFieldDisabled -= OnInputFieldDisabled;
                CurrentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
                CurrentChatBox.OnInputFieldCommandSubmitted -= ChatBoxOnOnInputFieldCommandSubmitted;
                CurrentChatBox.OnInputFieldUpdated -= CurrentChatBoxOnOnInputFieldUpdated;

                CurrentChatBox.Disable();
            }

            CurrentChatBox = chatBox;
            _isGlobalChatActive = CurrentChatBox == globalChatBox;
            globalChatBox.gameObject.SetActive(CurrentChatBox == globalChatBox);
            lobbyChatBox.gameObject.SetActive(CurrentChatBox == lobbyChatBox);

            CurrentChatBox.OnExtraImageInteract += SendMessage;
            CurrentChatBox.OnInputFieldEnabled += OnInputFieldEnabled;
            CurrentChatBox.OnInputFieldDisabled += OnInputFieldDisabled;
            CurrentChatBox.OnInputFieldSubmitted += OnInputFieldSubmittedCurrentBox;
            CurrentChatBox.OnInputFieldCommandSubmitted += ChatBoxOnOnInputFieldCommandSubmitted;
            CurrentChatBox.OnInputFieldUpdated += CurrentChatBoxOnOnInputFieldUpdated;

            CurrentChatBox.EnableInputField();
            CurrentChatBox.Enable();
        }

        private void CurrentChatBoxOnOnInputFieldUpdated(string obj)
        {
            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.ShowCursor();
            }
        }

        private void SendMessage()
        {
            CurrentChatBox.DisableInputField();
        }

        private void OnInputFieldEnabled()
        {
            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.ShowCursor();
            }

            PlayerInput.Instance.SwitchChatButton.gameObject.SetActive(true);
        }

        private void OnInputFieldDisabled()
        {
            if (CursorManager.Instance != null)
            {
                CursorManager.Instance.HideCursor();
            }

            PlayerInput.Instance.SwitchChatButton.gameObject.SetActive(false);
        }

        private void ChatBoxOnOnInputFieldCommandSubmitted(string command, string message)
        {
            if (!CommandsDictionary.TryGetValue(command, out var commandData))
            {
                SendSystemMessage("command not found", UltimateChatBoxStyles.errorMessage);
                return;
            }

            if (commandData.requireMessageValue && string.IsNullOrEmpty(message))
            {
                SendSystemMessage("command need value", UltimateChatBoxStyles.errorMessage);
                return;
            }

            commandData.unityEvent?.Invoke(message);
        }

        private void OnMessageRecived(byte[] byteData)
        {
            var dataText = System.Text.Encoding.UTF8.GetString(byteData);
            var reciveData = JsonUtility.FromJson<ChatModel<Empty>>(dataText);
            ChatModel<NewMessageData> chatMessageData;
            ChatModel<Error> error;
            if (reciveData.@event is ChatSocketEvents.NewMessage or ChatSocketEvents.NewImportantMessage)
                chatMessageData = JsonUtility.FromJson<ChatModel<NewMessageData>>(dataText);
            else if (reciveData.@event is ChatSocketEvents.Error)
            {
                error = JsonUtility.FromJson<ChatModel<Error>>(dataText);
                SendSystemMessage(error.data.message, UltimateChatBoxStyles.errorMessage);
                return;
            }
            else
                return;

            if (chatMessageData.data.message.lobby_id == LobbyVariables.Instance.currentLobby.lobbyId)
                HandleLobbyMassage(chatMessageData.data.message);
            HandleGlobalMassage(chatMessageData.data.message);
        }

        private void OnInputFieldSubmittedCurrentBox(string text)
        {
            if (CurrentChatBox.InputFieldContainsCommand)
                return;

            var sendMessageModel = new ChatModel<SendMassage>
            {
                @event = ChatSocketEvents.SendMessage,
                data = new SendMassage
                {
                    lobby_id = _isGlobalChatActive ? "main" : LobbyVariables.Instance.currentLobby.lobbyId,
                    message = text,
                    type = "message"
                }
            };

            var stringToSend = JsonUtility.ToJson(sendMessageModel);

            chatWebSocket.SendText(stringToSend);
        }

        public void HandleGlobalMassage(MessageData message)
        {
            var idPrefix = message.lobby_id.Length > 4 ? "..." : "";
            var usernamePrefix = message.lobby_id == "main"
                ? ""
                : $"[{idPrefix}{message.lobby_id.Substring(message.lobby_id.Length - 4)}]";
            globalChatBox.RegisterChat($"{usernamePrefix}{message.user.username}",
                message.message);
        }

        public void HandleLobbyMassage(MessageData message)
        {
            lobbyChatBox.RegisterChat($"{message.user.username}", message.message);
        }

        public void SendSystemMessage(string message, UltimateChatBox.ChatStyle style)
        {
            lobbyChatBox.RegisterChat(systemName, message, style);
            globalChatBox.RegisterChat(systemName, message, style);
        }
    }
}