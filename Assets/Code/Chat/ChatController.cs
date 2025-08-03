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
            Debug.Log("OnEnable");
            chatWebSocket = new WebSocket($"ws://back.nexusmetaclub.com/api/client/ws/lobby?jwt={ClientDataStorage.AccessToken}&lobby_id={"main"}");
            
            chatWebSocket.OnMessage += OnMessageRecived;
            chatWebSocket.OnError += Debug.LogError;
            chatWebSocket.OnOpen += () => chatWebSocket.SendText("{\n  \"event\": \"send_message\",\n  \"data\": {\n    \"lobby_id\": \"main\",\n    \"message\": \"Привет всем!\",\n    \"type\": \"message\"\n  }\n}");
            
            chatWebSocket.Connect();
        }

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.Tab) && CurrentChatBox.IsEnabled)
                ChangeChat();
            
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
            Debug.Log(dataText);
            var reciveData = JsonUtility.FromJson<ChatModel<Empty>>(dataText);
            ChatModel<NewMessageData> chatMessageData;
            if(reciveData.@event is ChatSocketEvents.NewMessage or ChatSocketEvents.NewImportantMessage)
                chatMessageData =  JsonUtility.FromJson<ChatModel<NewMessageData>>(dataText);
            else 
                return;
            
            if(chatMessageData.data.message.lobby_id == LobbyVariables.Instance.currentLobby.lobbyId)
                HandleLobbyMassage(chatMessageData.data.message);
            HandleGlobalMassage(chatMessageData.data.message);
        }

        private void OnInputFieldSubmittedCurrentBox(string text)
        {
            if (CurrentChatBox.InputFieldContainsCommand)
                return;
            
            //TODO: Send

            var sendMessageModel = new ChatModel<SendMassage>
            {
                @event = ChatSocketEvents.SendMessage,
                data = new SendMassage
                {
                    lobby_id = LobbyVariables.Instance.currentLobby.lobbyId,
                    message = text,
                    type = "message"
                }
            };

            var stringToSend = JsonUtility.ToJson(sendMessageModel);
            Debug.Log(stringToSend);

            chatWebSocket.SendText(stringToSend);
        }

        public void HandleGlobalMassage(MessageData message)
        {
            globalChatBox.RegisterChat($"[...{message.lobby_id.Substring(message.lobby_id.Length - 4)}]{message.user.username}",
                message.message);
        }

        public void HandleLobbyMassage(MessageData message)
        {
            lobbyChatBox.RegisterChat($"{message.user.username}", message.message);
        }
    }
}