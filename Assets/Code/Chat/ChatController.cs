using System;
using System.Collections.Generic;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using NativeWebSocket;
using Proyecto26;
using System.Threading.Tasks;
using Code.UI;
using UnityEngine;

namespace Code.Chat
{
    public class ChatController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Chat System")] [SerializeField]
        private ChatSystemUI chatSystemUI;

        [Header("Network")] [SerializeField] private float socketReconnectTimeout = 10f;

        [Header("Settings")] [SerializeField] private string systemName = "[SYSTEM]";
        [SerializeField] private List<CommandData> commands;
        [SerializeField] private int pageSize = 50;
        [SerializeField] private bool devLog = false;

        #endregion

        #region Public Properties

        public readonly Dictionary<string, CommandData> CommandsDictionary = new();
        public string SystemName => systemName;
        public bool IsMuted { get; set; }
        public ChatType CurrentChatType => chatSystemUI?.CurrentChatType ?? ChatType.Lobby;

        #endregion

        #region Private Fields

        private WebSocket _ws;
        private float _pingInterval = 5f;
        private float _pingTimer;
        private bool _historyLoading;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (devLog) Debug.Log("[ChatController] Awake");
            InitializeCommands();
        }

        private void Start()
        {
            if (devLog) Debug.Log("[ChatController] Start");

            if (chatSystemUI == null)
            {
                Debug.LogError("[ChatController] ChatSystemUI не назначен!");
                enabled = false;
                return;
            }

            SubscribeToUIEvents();
        }

        private void OnEnable()
        {
            if (chatSystemUI == null) return;

            if (devLog) Debug.Log($"[ChatController] OnEnable");
            InitializeWebSocket();
        }

        private void OnDisable()
        {
            UnsubscribeFromUIEvents();
            CloseWebSocket();
        }

        private void Update()
        {
            HandleInput();
            HandleWebSocket();
        }

        #endregion

        #region Initialization

        private void InitializeCommands()
        {
            CommandsDictionary.Clear();
            if (commands != null)
            {
                foreach (var command in commands)
                {
                    if (command != null && !string.IsNullOrEmpty(command.commandValue))
                    {
                        if (!CommandsDictionary.ContainsKey(command.commandValue))
                            CommandsDictionary.Add(command.commandValue, command);
                    }
                }
            }

            if (devLog) Debug.Log($"[ChatController] Initialized {CommandsDictionary.Count} commands");
        }

        private void SubscribeToUIEvents()
        {
            if (chatSystemUI == null) return;

            chatSystemUI.OnMessageSent += OnMessageSent;
            chatSystemUI.OnCommandSent += OnCommandSent;
            chatSystemUI.OnChatTypeChanged += OnChatTypeChanged;
            chatSystemUI.OnInputFieldEnabled += OnInputFieldEnabled;
            chatSystemUI.OnInputFieldDisabled += OnInputFieldDisabled;
            chatSystemUI.OnReachedTop += OnReachedTop;

            if (devLog) Debug.Log("[ChatController] Subscribed to UI events");
        }

        private void UnsubscribeFromUIEvents()
        {
            if (chatSystemUI == null) return;

            chatSystemUI.OnMessageSent -= OnMessageSent;
            chatSystemUI.OnCommandSent -= OnCommandSent;
            chatSystemUI.OnChatTypeChanged -= OnChatTypeChanged;
            chatSystemUI.OnInputFieldEnabled -= OnInputFieldEnabled;
            chatSystemUI.OnInputFieldDisabled -= OnInputFieldDisabled;
            chatSystemUI.OnReachedTop -= OnReachedTop;

            if (devLog) Debug.Log("[ChatController] Unsubscribed from UI events");
        }

        private void InitializeWebSocket()
        {
            string jwt = ClientDataStorage.AccessToken ?? string.Empty;
            string url = $"wss://back.nexusmetaclub.com/api/client/lobby/chat/ws?jwt={jwt}&lobby_id=main";

            _ws = new WebSocket(url);
            _ws.OnOpen += OnWsOpen;
            _ws.OnMessage += OnWsMessage;
            _ws.OnError += OnWsError;
            _ws.OnClose += OnWsClose;
            _ws.Connect();

            if (devLog) Debug.Log("[ChatController] WebSocket connecting...");
        }

        private void CloseWebSocket()
        {
            if (_ws != null)
            {
                _ws.OnOpen -= OnWsOpen;
                _ws.OnMessage -= OnWsMessage;
                _ws.OnError -= OnWsError;
                _ws.OnClose -= OnWsClose;

                try
                {
                    _ws.Close();
                }
                catch (Exception ex)
                {
                    if (devLog) Debug.LogWarning($"[ChatController] WebSocket close error: {ex.Message}");
                }
                finally
                {
                    _ws = null;
                }
            }

            if (devLog) Debug.Log("[ChatController] WebSocket closed");
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            var input = PlayerInput.Instance;
            if (input == null) return;

            var paused = false;
            if (PauseUI.Instance != null)
                paused = PauseUI.Instance.IsPaused;

            // Открытие/закрытие чата
            if (input.IsOpenChatDown && !paused && !input.IsBusy)
                ToggleChat();

            // Переключение между типами чата
            if (input.IsSwitchChatDown && chatSystemUI.IsVisible)
                chatSystemUI.SwitchChatType();

            // Закрытие чата по Escape
            if (input.IsPausedDown && chatSystemUI.IsVisible)
                chatSystemUI.Hide();

            // Скроллинг
            if (input.IsScrollUpButton && chatSystemUI.IsVisible)
                chatSystemUI.ScrollUp();

            if (input.IsScrollDownButton && chatSystemUI.IsVisible)
                chatSystemUI.ScrollDown();

            // Отправка сообщения (мобильные устройства)
            if (input.SendChatMessageButtonDown)
                SendCurrentMessage();

            // Синхронизация состояния
            PlayerInput.Instance.IsChatOpened = chatSystemUI.InputFieldActive;

            if (paused && chatSystemUI.IsVisible)
            {
                chatSystemUI.Hide();
            }
        }

        private void ToggleChat()
        {
            if (chatSystemUI.IsVisible)
            {
                // chatSystemUI.Hide();
            }
            else
            {
                chatSystemUI.Show();
                chatSystemUI.EnableInputField();
                LoadHistoryIfNeeded();
            }
        }

        private void SendCurrentMessage()
        {
            if (!string.IsNullOrEmpty(chatSystemUI.InputText))
            {
                OnMessageSent(chatSystemUI.InputText);
                chatSystemUI.InputText = "";
            }
        }

        #endregion

        #region WebSocket Handling

        private void HandleWebSocket()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif

            if (_ws != null && _ws.State == WebSocketState.Open)
                Ping();
        }

        private void Ping()
        {
            _pingTimer += Time.deltaTime;
            if (_pingTimer >= _pingInterval)
            {
                _pingTimer = 0f;
                try
                {
                    _ws.SendText("{\"event\":\"ping\",\"data\":{}}");
                }
                catch (Exception ex)
                {
                    if (devLog) Debug.LogWarning($"[ChatController] Ping error: {ex.Message}");
                }
            }
        }

        private void OnWsOpen()
        {
            SendSystemMessage("Connected to chat server", ChatStyles.Notice);
            if (devLog) Debug.Log("[ChatController] WebSocket opened");
        }

        private void OnWsClose(WebSocketCloseCode code)
        {
            SendSystemMessage($"Disconnected from chat (code: {(int)code})", ChatStyles.Warning);
            if (devLog) Debug.Log($"[ChatController] WebSocket closed: {code}");

            if (code != WebSocketCloseCode.Normal)
                Invoke(nameof(TryReconnect), socketReconnectTimeout);
        }

        private void TryReconnect()
        {
            try
            {
                SendSystemMessage("Reconnecting to chat...", ChatStyles.Notice);
                _ws?.Connect();
            }
            catch (Exception ex)
            {
                if (devLog) Debug.LogError($"[ChatController] Reconnect failed: {ex.Message}");
            }
        }

        private void OnWsError(string err)
        {
            SendSystemMessage(err ?? "Chat connection error", ChatStyles.Error);
            if (devLog) Debug.LogError($"[ChatController] WebSocket error: {err}");
        }

        private void OnWsMessage(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            string text;
            try
            {
                text = System.Text.Encoding.UTF8.GetString(data);
            }
            catch
            {
                return;
            }

            var envelope = JsonUtility.FromJson<ChatModel<object>>(text);
            if (envelope == null || string.IsNullOrEmpty(envelope.@event)) return;

            switch (envelope.@event)
            {
                case ChatSocketEvents.NewMessage:
                case ChatSocketEvents.NewImportantMessage:
                    HandleNewMessage(text);
                    break;
                case ChatSocketEvents.Error:
                    HandleErrorMessage(text);
                    break;
                case ChatSocketEvents.LikeToggled:
                case ChatSocketEvents.MessageLikeUpdated:
                    HandleLikeChanges(text);
                    break;
                case ChatSocketEvents.ViewMarked:
                case ChatSocketEvents.MessageViewsUpdated:
                    HandleViewChange(text); 
                    break;
            }
        }

        private void HandleNewMessage(string text)
        {
            Debug.Log(text);
            var msg = JsonUtility.FromJson<ChatModel<NewMessageData>>(text);
            if (msg?.data?.message == null) return;

            var message = msg.data.message;
            var lobby = LobbyVariables.Instance?.currentLobby;

            // Определяем тип чата для сообщения
            bool isLobbyMessage = lobby != null && message.lobby_id == lobby.lobbyId;
            bool isGlobalMessage = message.lobby_id == "main" || !isLobbyMessage;

            var messageStyle = message.type == "important" ? ChatStyles.Important : ChatStyles.Default;
            
            // ВСЕГДА добавляем сообщение в глобальный чат
            var lobbyId = message.lobby_id ?? "main";
            var prefix = GetLobbyPrefix(lobbyId);

            /*chatSystemUI.AddMessage(
                $"{prefix}{message.user.username}",
                message.message,
                ChatType.Global,
                messageStyle,
                message.id
            );*/
            
            chatSystemUI.AddMessage(new ChatMessage
            {
                displayUsername = $"{prefix}{message.user.username}",
                displayMessage = message.message,
                chatType = ChatType.Global,
                timestamp = DateTime.Parse(message.created_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                style = messageStyle,
                chatMessageData = message
            }, ClientDataStorage.UserData.id == message.user_id);

            // Если сообщение из текущего лобби, добавляем его также в чат лобби
            if (isLobbyMessage)
            {
                chatSystemUI.AddMessage(new ChatMessage
                {
                    displayUsername = message.user.username,
                    displayMessage = message.message,
                    chatType = ChatType.Lobby,
                    timestamp = DateTime.Parse(message.created_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                    style = messageStyle,
                    chatMessageData = message
                }, ClientDataStorage.UserData.id == message.user_id);
            }
        }

        private void HandleErrorMessage(string text)
        {
            var err = JsonUtility.FromJson<ChatModel<Error>>(text);
            if (err?.data != null)
                SendSystemMessage(err.data.message, ChatStyles.Error);
        }

        private string GetLobbyPrefix(string lobbyId)
        {
            if (lobbyId == "main") return "";

            var suffix = lobbyId.Length > 4 ? "..." : "";
            return $"[{suffix}{lobbyId.Substring(Mathf.Max(0, lobbyId.Length - 4))}]";
        }

        private void HandleViewChange(string text)
        {
            var data = JsonUtility.FromJson<ChatModel<ViewUpdatedModel>>(text);
            if(data != null)
                chatSystemUI.OnViewUpdated(data.data);
        }

        private void HandleLikeChanges(string text)
        {
            var data = JsonUtility.FromJson<ChatModel<LikeUpdatedModel>>(text);
            if(data != null)
                chatSystemUI.OnLikeUpdated(data.data);
        }

        #endregion

        #region UI Event Handlers

        private void OnMessageSent(string message)
        {
            if (IsMuted)
            {
                SendSystemMessage("You are muted in chat", ChatStyles.Error);
                return;
            }

            var lobby = LobbyVariables.Instance?.currentLobby;
            string lobbyId = CurrentChatType == ChatType.Global ? "main" : (lobby?.lobbyId ?? "main");

            var payload = new ChatModel<SendMassage>
            {
                @event = ChatSocketEvents.SendMessage,
                data = new SendMassage
                {
                    lobby_id = lobbyId,
                    message = message,
                    type = "message"
                }
            };

            var json = JsonUtility.ToJson(payload);
            try
            {
                _ws?.SendText(json);
                if (devLog) Debug.Log($"[ChatController] Message sent to {CurrentChatType}: {message}");
            }
            catch (Exception ex)
            {
                SendSystemMessage("Failed to send message", ChatStyles.Error);
                if (devLog) Debug.LogError($"[ChatController] Send error: {ex.Message}");
            }
        }

        private void OnCommandSent(string command, string message)
        {
            if (string.IsNullOrEmpty(command))
            {
                SendSystemMessage("Command is empty", ChatStyles.Error);
                return;
            }

            if (!CommandsDictionary.TryGetValue(command, out var commandData) || commandData == null)
            {
                SendSystemMessage($"Unknown command: /{command}", ChatStyles.Error);
                return;
            }

            if (commandData.requireMessageValue && string.IsNullOrEmpty(message))
            {
                SendSystemMessage($"Command /{command} requires a value", ChatStyles.Error);
                return;
            }

            commandData.unityEvent?.Invoke(message);
            if (devLog) Debug.Log($"[ChatController] Command executed: /{command} {message}");
        }

        private void OnChatTypeChanged(ChatType newChatType)
        {
            if (devLog) Debug.Log($"[ChatController] Chat type changed to: {newChatType}");
            LoadHistoryIfNeeded();
        }

        private void OnInputFieldEnabled()
        {
            SetActiveMobileInput(true);
            if (devLog) Debug.Log("[ChatController] Input field enabled");
        }

        private void OnInputFieldDisabled()
        {
            SetActiveMobileInput(false);
            if (devLog) Debug.Log("[ChatController] Input field disabled");
        }

        private void OnReachedTop(ChatType chatType)
        {
            if (!_historyLoading)
            {
                _ = LoadHistoryPageAsync(false, chatType);
            }
        }

        #endregion

        #region History Loading

        private void LoadHistoryIfNeeded()
        {
            var currentType = CurrentChatType;
            if (!chatSystemUI.NoMoreHistory && chatSystemUI.CurrentChatMessagesCount < pageSize)
            {
                _ = LoadHistoryPageAsync(true, currentType);
            }
        }

        private async Task LoadHistoryPageAsync(bool reset, ChatType chatType)
        {
            if (_historyLoading || chatSystemUI.NoMoreHistory) return;

            _historyLoading = true;

            try
            {
                var isGlobal = chatType == ChatType.Global;
                var beforeId = reset ? null : chatSystemUI.OldestMessageId;

                var (items, hasMore) = await FetchHistoryAsync(beforeId, isGlobal);

                if (items == null || items.Length == 0)
                {
                    if (!hasMore) chatSystemUI.NoMoreHistory = true;
                    return;
                }

                var messages = new List<ChatMessage>();

                foreach (var item in items)
                {
                    var username = !string.IsNullOrEmpty(item.user.username)
                        ? item.user.username
                        : (item.user_id != 0 ? "User#" + item.user_id : "User");

                    var style = item.type == "important" ? ChatStyles.Important : ChatStyles.Default;

                    if (isGlobal)
                    {
                        var prefix = GetLobbyPrefix(item.lobby_id ?? "main");
                        username = $"{prefix}{username}";
                    }

                    messages.Add(new ChatMessage
                    {
                        displayUsername = username,
                        displayMessage = item.message ?? string.Empty,
                        style = style,
                        timestamp = DateTime.Parse(item.created_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                        chatType = chatType,
                        chatMessageData = item
                    });
                }

                chatSystemUI.PrependMessages(messages, chatType);

                if (!hasMore)
                    chatSystemUI.NoMoreHistory = true;

                if (devLog) Debug.Log($"[ChatController] Loaded {messages.Count} history messages for {chatType}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatController] History loading error: {ex.Message}");
            }
            finally
            {
                _historyLoading = false;
            }
        }

        private async Task<(MessageData[] items, bool hasMore)> FetchHistoryAsync(long? beforeId, bool isGlobal)
        {
            var url = ApiRoutes.DOMAIN.TrimEnd('/') + "/api/client/lobby/chat/messages";
            var reqParams = new Dictionary<string, string> { { "limit", pageSize.ToString() } };

            if (!isGlobal)
            {
                var lobby = LobbyVariables.Instance?.currentLobby;
                if (lobby != null)
                    reqParams.Add("lobby_id", lobby.lobbyId);
            }

            if (beforeId.HasValue)
                reqParams.Add("before_id", beforeId.Value.ToString());

            var req = new RequestHelper
            {
                Uri = url,
                Method = "GET",
                Headers = ClientDataStorage.GetJwtHeader(),
                Params = reqParams
            };

            try
            {
                var resp = await RestClient.Request(req).ToTask();

                if (resp.StatusCode >= 400)
                {
                    Debug.LogWarning($"[ChatController] History HTTP {resp.StatusCode}");
                    return (null, false);
                }

                var text = resp.Text ?? string.Empty;
                var env = JsonUtility.FromJson<SuccessResponse<HistoryEnvelopeData>>(text);

                if (env?.success != true || env.data?.messages == null)
                {
                    Debug.LogWarning($"[ChatController] History parse fail");
                    return (null, false);
                }

                Array.Reverse(env.data.messages);
                return (env.data.messages, env.data.has_more);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatController] History fetch error: {ex.Message}");
                return (null, false);
            }
        }

        #endregion

        #region Utility Methods

        public void SendSystemMessage(string message, ChatMessageStyle style)
        {
            chatSystemUI.AddMessage(new ChatMessage
            {
                displayUsername = systemName,
                displayMessage = message,
                chatType = ChatType.Lobby,
                style = style
            });
            chatSystemUI.AddMessage(new ChatMessage
            {
                displayUsername = systemName,
                displayMessage = message,
                chatType = ChatType.Global,
                style = style
            });
        }

        private void SetActiveMobileInput(bool value)
        {
            var playerInput = PlayerInput.Instance;
            if (playerInput == null) return;

            if (playerInput.SwitchChatButton != null)
                playerInput.SwitchChatButton.gameObject.SetActive(value);

            if (playerInput.ChatScrollUpButton != null)
                playerInput.ChatScrollUpButton.gameObject.SetActive(value);

            if (playerInput.ChatScrollDownButton != null)
                playerInput.ChatScrollDownButton.gameObject.SetActive(value);

            if (playerInput.SendChatMessageButton != null)
                playerInput.SendChatMessageButton.gameObject.SetActive(value);
        }

        #endregion

        #region Public API

        public void MoveUp() => chatSystemUI?.ScrollUp();
        public void MoveDown() => chatSystemUI?.ScrollDown();

        [ContextMenu("Dev: Load History")]
        public void Dev_LoadHistoryNow()
        {
            if (devLog) Debug.Log("[ChatController] Manual history load");
            _ = LoadHistoryPageAsync(false, CurrentChatType);
        }

        public void OnViewMessage(long id)
        {
            var dataForSend = new ChatModel<ReactBaseModel>
            {
                @event = ChatSocketEvents.MarkViewed,
                data = new ReactBaseModel
                {
                    message_id = id
                }
            };
            
            _ws?.SendText(JsonUtility.ToJson(dataForSend));
        }

        public void OnLikeMessage(long id, bool like)
        {
            var dataForSend = new ChatModel<LikeSendModel>
            {
                @event = ChatSocketEvents.ToggleLike,
                data = new LikeSendModel
                {
                    message_id = id,
                    like = like
                }
            };
            
            _ws?.SendText(JsonUtility.ToJson(dataForSend));
        }

        #endregion
    }
}