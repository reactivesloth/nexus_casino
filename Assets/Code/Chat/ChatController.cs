using System;
using System.Collections.Generic;
using System.Linq;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using NativeWebSocket;
using Proyecto26;
using System.Threading.Tasks;
using Code.Utility;
using TankAndHealerStudioAssets;
using UnityEngine;

namespace Code.Chat
{
    public class ChatController : MonoBehaviour
    {
        [SerializeField] private float socketReconnectTimeout = 10f;
        [SerializeField] private string systemName = "[SYSTEM]";
        [SerializeField] private UltimateChatBox lobbyChatBox;
        [SerializeField] private UltimateChatBox globalChatBox;
        [SerializeField] private List<CommandData> commands;

        [Header("ChatPosition settings")] [SerializeField]
        private Vector2 desktopPosition;

        [SerializeField] private Vector2 mobilePosition;

        public readonly Dictionary<string, CommandData> CommandsDictionary = new();

        private WebSocket _ws;
        private float _pingInterval = 5f;
        private float _pingTimer;
        private bool _isGlobalChatActive;

        public UltimateChatBox CurrentChatBox;
        public string SystemName => systemName;
        public bool IsMuted { get; set; }

        // ===== История =====
        [Header("History")] [SerializeField] private int pageSize = 50;
        private bool _historyLoading;
        [SerializeField] private bool devLog; // в инспекторе поставь галочку, чтобы включить логи

        private void Awake()
        {
            if (devLog) Debug.Log("[CHAT] Awake");
            if (PlayerInput.Instance.IsUsingMobileFallback)
            {
                lobbyChatBox.useExtraImage = false;
                lobbyChatBox.useExtraImage = false;
                globalChatBox.useExtraImage = false;
                globalChatBox.useExtraImage = false;
            }
        }

        private void Start()
        {
            if (devLog) Debug.Log("[CHAT] Start");

            CommandsDictionary.Clear();
            if (commands != null)
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    var c = commands[i];
                    if (c == null || string.IsNullOrEmpty(c.commandValue)) continue;
                    if (!CommandsDictionary.ContainsKey(c.commandValue))
                        CommandsDictionary.Add(c.commandValue, c);
                }
            }

            var isMobile = PlayerInput.Instance != null && PlayerInput.Instance.IsUsingMobileFallback;
            var pos = isMobile ? mobilePosition : desktopPosition;

            if (lobbyChatBox != null)
            {
                lobbyChatBox.chatBoxPosition = pos;
                lobbyChatBox.UpdatePositioning();
            }

            if (globalChatBox != null)
            {
                globalChatBox.chatBoxPosition = pos;
                globalChatBox.UpdatePositioning();
            }
        }

        private void OnEnable()
        {
            if (lobbyChatBox == null || globalChatBox == null) return;
            if (devLog) Debug.Log($"[CHAT] OnEnable (goActive={gameObject.activeInHierarchy}, compEnabled={enabled})");

            SetCurrentChat(lobbyChatBox);
            CurrentChatBox.Disable();
            CurrentChatBox.DisableInputField();

            string jwt = ClientDataStorage.AccessToken ?? string.Empty;
            string url = $"wss://back.nexusmetaclub.com/api/client/ws/lobby?jwt={jwt}&lobby_id=main";

            _ws = new WebSocket(url);
            _ws.OnOpen += OnWsOpen;
            _ws.OnMessage += OnWsMessage;
            _ws.OnError += OnWsError;
            _ws.OnClose += OnWsClose;
            _ws.Connect();

            SetActiveMobileInput(false);
        }

        private void OnDisable()
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
                catch
                {
                }

                _ws = null;
            }

            if (CurrentChatBox != null)
            {
                CurrentChatBox.OnExtraImageInteract -= SendMessage;
                CurrentChatBox.OnInputFieldEnabled -= OnInputFieldEnabled;
                CurrentChatBox.OnInputFieldDisabled -= OnInputFieldDisabled;
                CurrentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
                CurrentChatBox.OnInputFieldCommandSubmitted -= ChatBoxOnOnInputFieldCommandSubmitted;
                CurrentChatBox.OnInputFieldUpdated -= CurrentChatBoxOnOnInputFieldUpdated;
                // отписка от события верха
                try
                {
                    CurrentChatBox.ReachedTop -= OnReachedTopLoadHistory;
                }
                catch
                {
                }
            }
        }

        private void Update()
        {
            var input = PlayerInput.Instance;
            if (input != null)
            {
                if (input.IsOpenChatDown) OpenChat();
                if (input.IsSwitchChatDown && CurrentChatBox != null && CurrentChatBox.IsEnabled) ChangeChat();
                if (input.IsPausedDown && CurrentChatBox != null) CurrentChatBox.Disable();
                if (input.IsScrollUpButton) MoveUp();
                if (input.IsScrollDownButton) MoveDown();
                if(input.SendChatMessageButtonDown) SendMessage();
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
            if (_ws != null && _ws.State == WebSocketState.Open)
                Ping();
        }

        public void MoveUp()
        {
            CurrentChatBox.ScrollUp();
        }

        public void MoveDown()
        {
            CurrentChatBox.ScrollDown();
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
                catch
                {
                }
            }
        }

        // === WebSocket events ===
        private void OnWsOpen()
        {
            SendSystemMessage("Chat connection open.", UltimateChatBoxStyles.noticeMessage);
        }

        private void OnWsClose(WebSocketCloseCode code)
        {
            SendSystemMessage($"Chat connection close, code {(int)code} {code}", UltimateChatBoxStyles.noticeMessage);
            if (code != WebSocketCloseCode.Normal)
                Invoke(nameof(TryReconnect), socketReconnectTimeout);
        }

        private void TryReconnect()
        {
            try
            {
                SendSystemMessage("Try reconnect...", UltimateChatBoxStyles.noticeMessage);
                _ws?.Connect();
            }
            catch
            {
            }
        }

        private void OnWsError(string err)
        {
            SendSystemMessage(err ?? "Chat socket error", UltimateChatBoxStyles.errorMessage);
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

            var envelope = JsonUtility.FromJson<ChatModel<Empty>>(text);
            if (envelope == null || string.IsNullOrEmpty(envelope.@event)) return;

            if (envelope.@event == ChatSocketEvents.NewMessage ||
                envelope.@event == ChatSocketEvents.NewImportantMessage)
            {
                var msg = JsonUtility.FromJson<ChatModel<NewMessageData>>(text);
                if (msg?.data?.message != null)
                {
                    var m = msg.data.message;

                    var lobby = LobbyVariables.Instance != null ? LobbyVariables.Instance.currentLobby : null;

                    if (lobby != null && m.lobby_id == lobby.lobbyId)
                        HandleLobbyMassage(m);

                    HandleGlobalMassage(m);
                }
            }
            else if (envelope.@event == ChatSocketEvents.Error)
            {
                var err = JsonUtility.FromJson<ChatModel<Error>>(text);
                if (err != null && err.data != null)
                    SendSystemMessage(err.data.message, UltimateChatBoxStyles.errorMessage);
            }
        }

        private void OpenChat()
        {
            if (CurrentChatBox == null) return;
            bool open = !CurrentChatBox.IsEnabled;
            Debug.Log(open);

            if (open)
            {
                CurrentChatBox.Enable();
                CurrentChatBox.EnableInputField();
            }
            else
            {
                if(!PlayerInput.Instance.IsUsingMobileFallback)
                    CurrentChatBox.DisableInputField();
                CurrentChatBox.Disable();
            }
        }

        private void ChangeChat()
        {
            if (CurrentChatBox == null || lobbyChatBox == null || globalChatBox == null) return;
            SetCurrentChat(CurrentChatBox == lobbyChatBox ? globalChatBox : lobbyChatBox);
        }

        private void SetCurrentChat(UltimateChatBox chatBox)
        {
            if (chatBox == null) return;
            if (devLog)
                Debug.Log(
                    $"[CHAT] SetCurrentChat called; chatBox={(chatBox ? chatBox.name : "")}, Inited={chatBox?.WasInitLoad}");

            if (CurrentChatBox != null)
            {
                CurrentChatBox.OnExtraImageInteract -= SendMessage;
                CurrentChatBox.OnInputFieldEnabled -= OnInputFieldEnabled;
                CurrentChatBox.OnInputFieldDisabled -= OnInputFieldDisabled;
                CurrentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
                CurrentChatBox.OnInputFieldCommandSubmitted -= ChatBoxOnOnInputFieldCommandSubmitted;
                CurrentChatBox.OnInputFieldUpdated -= CurrentChatBoxOnOnInputFieldUpdated;
                CurrentChatBox.InputField.onTouchScreenKeyboardStatusChanged.RemoveListener(OnKeyboardStatusChanged);

                try
                {
                    CurrentChatBox.ReachedTop -= OnReachedTopLoadHistory;
                }
                catch
                {
                }

                CurrentChatBox.Disable();
            }

            CurrentChatBox = chatBox;
            _isGlobalChatActive = (CurrentChatBox == globalChatBox);

            if (globalChatBox != null) globalChatBox.gameObject.SetActive(CurrentChatBox == globalChatBox);
            if (lobbyChatBox != null) lobbyChatBox.gameObject.SetActive(CurrentChatBox == lobbyChatBox);

            CurrentChatBox.OnExtraImageInteract += SendMessage;
            CurrentChatBox.OnInputFieldEnabled += OnInputFieldEnabled;
            CurrentChatBox.OnInputFieldDisabled += OnInputFieldDisabled;
            CurrentChatBox.OnInputFieldSubmitted += OnInputFieldSubmittedCurrentBox;
            CurrentChatBox.OnInputFieldCommandSubmitted += ChatBoxOnOnInputFieldCommandSubmitted;
            CurrentChatBox.OnInputFieldUpdated += CurrentChatBoxOnOnInputFieldUpdated;
            CurrentChatBox.InputField.onTouchScreenKeyboardStatusChanged.AddListener(OnKeyboardStatusChanged);

            try
            {
                CurrentChatBox.ReachedTop += OnReachedTopLoadHistory;
            }
            catch
            {
            }

            CurrentChatBox.NoMoreHistory = false;

            CurrentChatBox.EnableInputField();
            CurrentChatBox.Enable();

            if (!CurrentChatBox.WasInitLoad)
                _ = LoadHistoryPageAsync(true, _isGlobalChatActive);
        }

        private void OnKeyboardStatusChanged(TouchScreenKeyboard.Status newStatus)
        {
            /*if( newStatus is TouchScreenKeyboard.Status.Done or TouchScreenKeyboard.Status.Canceled)
                SendMessage();*/
        }

        [ContextMenu("Dev Load History Now")]
        public void Dev_LoadHistoryNow()
        {
            if (devLog) Debug.Log("[CHAT] Dev_LoadHistoryNow()");
            _ = LoadHistoryPageAsync(false, _isGlobalChatActive);
        }

        private void CurrentChatBoxOnOnInputFieldUpdated(string _)
        {
            if (CursorManager.Instance != null)
                CursorManager.Instance.ShowCursor();
        }

        private void SendMessage()
        {
            Debug.Log($"[CHAT] Send Message");
            CurrentChatBox.DisableInputField();
            CurrentChatBox.Disable();
        }

        private void OnInputFieldEnabled()
        {
            if (CursorManager.Instance != null)
                CursorManager.Instance.ShowCursor();

            SetActiveMobileInput(true);
        }

        private void OnInputFieldDisabled()
        {
            if (CursorManager.Instance != null)
                CursorManager.Instance.HideCursor();

            SetActiveMobileInput(false);
        }

        private void ChatBoxOnOnInputFieldCommandSubmitted(string command, string message)
        {
            if (string.IsNullOrEmpty(command))
            {
                SendSystemMessage("command is empty", UltimateChatBoxStyles.errorMessage);
                return;
            }

            if (!CommandsDictionary.TryGetValue(command, out var cd) || cd == null)
            {
                SendSystemMessage("command not found", UltimateChatBoxStyles.errorMessage);
                return;
            }

            if (cd.requireMessageValue && string.IsNullOrEmpty(message))
            {
                SendSystemMessage("command need value", UltimateChatBoxStyles.errorMessage);
                return;
            }

            cd.unityEvent?.Invoke(message);
        }

        private void OnInputFieldSubmittedCurrentBox(string text)
        {
            if (CurrentChatBox == null || CurrentChatBox.InputFieldContainsCommand) return;
            if (IsMuted)
            {
                SendSystemMessage("You are muted in chat", UltimateChatBoxStyles.errorMessage);
                return;
            }

            var lobby = LobbyVariables.Instance != null ? LobbyVariables.Instance.currentLobby : null;
            string lobbyId = _isGlobalChatActive ? "main" : (lobby != null ? lobby.lobbyId : "main");

            var payload = new ChatModel<SendMassage>
            {
                @event = ChatSocketEvents.SendMessage,
                data = new SendMassage { lobby_id = lobbyId, message = text, type = "message" }
            };

            var json = JsonUtility.ToJson(payload);
            try
            {
                _ws?.SendText(json);
            }
            catch
            {
            }
        }

        public void HandleGlobalMassage(MessageData m)
        {
            if (globalChatBox == null || m == null || string.IsNullOrEmpty(m.user.username)) return;

            string lobbyId = m.lobby_id ?? "main";
            string suffix = lobbyId.Length > 4 ? "..." : "";
            string prefix = lobbyId == "main" ? "" : $"[{suffix}{lobbyId.Substring(Mathf.Max(0, lobbyId.Length - 4))}]";
            globalChatBox.RegisterChat($"{prefix}{m.user.username}", m.message);
            SetLastChatInfo(m);
        }

        public void HandleLobbyMassage(MessageData m)
        {
            if (lobbyChatBox == null || m == null || string.IsNullOrEmpty(m.user.username)) return;
            lobbyChatBox.RegisterChat(m.user.username, m.message);
            SetLastChatInfo(m);
        }

        private void SetLastChatInfo(MessageData m)
        {
            var chatInfo = lobbyChatBox.ChatInformations.LastOrDefault();
            if (chatInfo != null)
                chatInfo.MessageId = m.id;
        }

        public void SendSystemMessage(string msg, UltimateChatBox.ChatStyle style)
        {
            if (lobbyChatBox != null) lobbyChatBox.RegisterChat(systemName, msg, style);
            if (globalChatBox != null) globalChatBox.RegisterChat(systemName, msg, style);
        }

        // ==== История: дотягивание вверх ====
        private void OnReachedTopLoadHistory()
        {
            if (!_historyLoading)
            {
                var _ = LoadHistoryPageAsync(false, _isGlobalChatActive);
            }
        }

        private async Task LoadHistoryPageAsync(bool reset, bool isGlobalChatActive)
        {
            var updatedChat = isGlobalChatActive ? globalChatBox : lobbyChatBox;
            if (_historyLoading || updatedChat.NoMoreHistory) return;
            _historyLoading = true;

            try
            {
                async Task<(MessageData[] items, bool hasMore, long minId)> FetchAsync(
                    long? beforeId)
                {
                    var url = ApiRoutes.DOMAIN.TrimEnd('/') +
                              "/api/client/lobby-messages";

                    var reqParams = new Dictionary<string, string> { { "limit", pageSize.ToString() } };

                    if (!isGlobalChatActive)
                        reqParams.Add("lobby_id", LobbyVariables.Instance.currentLobby.lobbyId);

                    if (beforeId.HasValue)
                        reqParams.Add("before_id", beforeId.Value.ToString());

                    var req = new RequestHelper
                    {
                        Uri = url,
                        Method = "GET",
                        Headers = ClientDataStorage.GetJwtHeader(),
                        Params = reqParams
                    };

                    var b_id_text = beforeId.HasValue ? beforeId.Value.ToString() : "null";
                    Debug.Log($"[CHAT] GET {req.Uri}, before_id={b_id_text}");

                    ResponseHelper resp;
                    try
                    {
                        resp = await RestClient.Request(req).ToTask();
                    }
                    catch
                    {
                        req.Headers = new Dictionary<string, string>
                        {
                            { "Authorization", "Bearer " + (ClientDataStorage.AccessToken ?? string.Empty) }
                        };
                        resp = await RestClient.Request(req).ToTask();
                    }

                    if (resp.StatusCode >= 400)
                    {
                        Debug.LogWarning($"[CHAT] History HTTP {resp.StatusCode}");
                        return (null, false, long.MaxValue);
                    }

                    var text = resp.Text ?? string.Empty;
                    var env = JsonUtility.FromJson<SuccessResponse<HistoryEnvelopeData>>(text);
                    if (env?.success != true || env.data?.messages == null)
                    {
                        Debug.LogWarning($"[CHAT] History parse fail or no data. Raw: {text}");
                        return (null, false, long.MaxValue);
                    }

                    var arr = env.data.messages;
                    var more = env.data.has_more;
                    var min = long.MaxValue;
                    for (var i = 0; i < arr.Length; i++)
                        if (arr[i].id > 0 && arr[i].id < min)
                            min = arr[i].id;

                    Debug.Log($"[CHAT] History items: {arr.Length}, has_more={more}, minId={min}");
                    return (arr, more, min);
                }

                Debug.Log($"[CHAT] Oldest message id is {updatedChat.OldestMessageId}");
                var beforeA =
                    (reset || !updatedChat.OldestMessageId.HasValue) ? null : updatedChat.OldestMessageId;

                var (itemsA, hasMoreA, minIdA) = await FetchAsync(beforeA);


                var pageItems = itemsA;
                var hasMore = hasMoreA;
                var minId = minIdA;

                if (pageItems == null || pageItems.Length == 0)
                {
                    if (!hasMore) updatedChat.NoMoreHistory = true;
                    return;
                }

                Array.Reverse(pageItems);

                var batch =
                    new List<(string username, string message, UltimateChatBox.ChatStyle, long id)>(pageItems.Length);
                for (var i = 0; i < pageItems.Length; i++)
                {
                    var m = pageItems[i];

                    Debug.Log(JsonUtility.ToJson(m));

                    var username = !string.IsNullOrEmpty(m.user.username) ? m.user.username :
                        m.user_id != 0 ? "User#" + m.user_id : "User";

                    var style = (m.type == "important")
                        ? UltimateChatBoxStyles.warningMessage
                        : UltimateChatBoxStyles.none;

                    var lobbyId = m.lobby_id ?? "main";
                    var suffix = lobbyId.Length > 4 ? "..." : "";
                    var prefix = lobbyId == "main"
                        ? ""
                        : $"[{suffix}{lobbyId.Substring(Mathf.Max(0, lobbyId.Length - 4))}] ";
                    prefix = isGlobalChatActive ? prefix : string.Empty;
                    batch.Add(($"{prefix}{username}", m.message ?? string.Empty, style, m.id));
                }

                if (!updatedChat.IsEnabled)
                    return;

                try
                {
                    updatedChat.PrependChats(batch);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CHAT] PrependChats error: {ex.Message}");
                }

                if (!hasMore) updatedChat.NoMoreHistory = true;
                updatedChat.WasInitLoad = true;
            }
            finally
            {
                Debug.Log("[CHAT] Loading end");
                _historyLoading = false;
            }
        }

        private void SetActiveMobileInput(bool value)
        {
            var playerInput = PlayerInput.Instance;
            if (playerInput == null)
                return;

            playerInput.SwitchChatButton.gameObject.SetActive(value);
            playerInput.ChatScrollUpButton.gameObject.SetActive(value);
            playerInput.ChatScrollDownButton.gameObject.SetActive(value);
            playerInput.SendChatMessageButton.gameObject.SetActive(value);
        }
    }
}