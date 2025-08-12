using System;
using System.Collections.Generic;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using Code.Player;
using NativeWebSocket;
using Proyecto26;
using System.Threading.Tasks;
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

        private WebSocket _ws;
        private float _pingInterval = 5f;
        private float _pingTimer;
        private bool _isGlobalChatActive;

        public UltimateChatBox CurrentChatBox { get; private set; }
        public string SystemName => systemName;

        // ===== История =====
        [Header("History")] [SerializeField] private int pageSize = 50;
        private bool _historyLoading;
        private bool _noMoreHistory;
        private int _oldestMessageId = int.MaxValue;
        private string _lastLiveLobbyId; // actual lobby_id из WS
        private long _lastLiveMessageId; // последний id из WS
        [SerializeField] private bool devLog;  // в инспекторе поставь галочку, чтобы включить логи

        private void Awake()
        {
            if (devLog) Debug.Log("[CHAT] Awake");
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
            string url = $"ws://back.nexusmetaclub.com/api/client/ws/lobby?jwt={jwt}&lobby_id=main";

            _ws = new WebSocket(url);
            _ws.OnOpen += OnWsOpen;
            _ws.OnMessage += OnWsMessage;
            _ws.OnError += OnWsError;
            _ws.OnClose += OnWsClose;
            _ws.Connect();

            if (PlayerInput.Instance != null && PlayerInput.Instance.SwitchChatButton != null)
                PlayerInput.Instance.SwitchChatButton.gameObject.SetActive(false);
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
            }

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
            {
                try
                {
                    _ws?.Connect();
                }
                catch
                {
                }
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

                    if (m != null)
                    {
                        if (!string.IsNullOrEmpty(m.lobby_id))
                            _lastLiveLobbyId = m.lobby_id;

                        if (m.id > 0)
                            _lastLiveMessageId = m.id;
                    }


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

        // === UI / ChatBox ===
        private void OpenChat()
        {
            if (CurrentChatBox == null) return;
            bool open = !CurrentChatBox.IsEnabled;

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
            if (CurrentChatBox == null || lobbyChatBox == null || globalChatBox == null) return;
            SetCurrentChat(CurrentChatBox == lobbyChatBox ? globalChatBox : lobbyChatBox);
        }

        private void SetCurrentChat(UltimateChatBox chatBox)
        {
            if (chatBox == null) return;
            if (devLog) Debug.Log($"[CHAT] SetCurrentChat called; chatBox={(chatBox ? chatBox.name : "")}");

            if (CurrentChatBox != null)
            {
                CurrentChatBox.OnExtraImageInteract -= SendMessage;
                CurrentChatBox.OnInputFieldEnabled -= OnInputFieldEnabled;
                CurrentChatBox.OnInputFieldDisabled -= OnInputFieldDisabled;
                CurrentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
                CurrentChatBox.OnInputFieldCommandSubmitted -= ChatBoxOnOnInputFieldCommandSubmitted;
                CurrentChatBox.OnInputFieldUpdated -= CurrentChatBoxOnOnInputFieldUpdated;

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

            // Хук бесконечной прокрутки вверх + сброс пагинации и первичная загрузка
            if (devLog) Debug.Log("[CHAT] Kick-off first history page (reset=true)");

            try { CurrentChatBox.ReachedTop -= OnReachedTopLoadHistory; } catch {}
            _historyLoading = true;
            _noMoreHistory = false;
            _oldestMessageId = int.MaxValue;

            var _ = LoadHistoryPageAsync(true).ContinueWith(__ =>
            {
                if (devLog) Debug.Log($"[CHAT] First page finished. Faulted={__.IsFaulted} Canceled={__.IsCanceled}");

                _historyLoading = false;
                if (!_noMoreHistory && CurrentChatBox != null)
                {
                    if (devLog) Debug.Log("[CHAT] Subscribing ReachedTop after first page.");
                    CurrentChatBox.ReachedTop += OnReachedTopLoadHistory;
                }
            });

            CurrentChatBox.EnableInputField();
            CurrentChatBox.Enable();
        }

        // РУЧНОЙ ТРИГГЕР из инспектора — нажми кнопку «Dev Load History Now»
        [ContextMenu("Dev Load History Now")]
        public void Dev_LoadHistoryNow()
        {
            if (devLog) Debug.Log("[CHAT] Dev_LoadHistoryNow()");
            _ = LoadHistoryPageAsync(false);
        }
        
        private void CurrentChatBoxOnOnInputFieldUpdated(string _)
        {
            if (CursorManager.Instance != null)
                CursorManager.Instance.ShowCursor();
        }

        private void SendMessage()
        {
            CurrentChatBox?.DisableInputField();
        }

        private void OnInputFieldEnabled()
        {
            if (CursorManager.Instance != null)
                CursorManager.Instance.ShowCursor();

            var pi = PlayerInput.Instance;
            if (pi != null && pi.SwitchChatButton != null)
                pi.SwitchChatButton.gameObject.SetActive(true);
        }

        private void OnInputFieldDisabled()
        {
            if (CursorManager.Instance != null)
                CursorManager.Instance.HideCursor();

            var pi = PlayerInput.Instance;
            if (pi != null && pi.SwitchChatButton != null)
                pi.SwitchChatButton.gameObject.SetActive(false);
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
            if (globalChatBox == null || m == null || m.user == null) return;

            string lobbyId = m.lobby_id ?? "main";
            string suffix = lobbyId.Length > 4 ? "..." : "";
            string prefix = lobbyId == "main" ? "" : $"[{suffix}{lobbyId.Substring(Mathf.Max(0, lobbyId.Length - 4))}]";
            globalChatBox.RegisterChat($"{prefix}{m.user.username}", m.message);
        }

        public void HandleLobbyMassage(MessageData m)
        {
            if (lobbyChatBox == null || m == null || m.user == null) return;
            lobbyChatBox.RegisterChat(m.user.username, m.message);
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
                var _ = LoadHistoryPageAsync(false);
            }
        }

        [Serializable]
        private class LobbyMessagesPageDto
        {
            public MessageData[] items;
            public bool has_more;
        }

        [Serializable]
        private class HistoryEnvelopeDto
        {
            public bool success;
            public HistoryEnvelopeData data;
        }

        [Serializable]
        private class HistoryEnvelopeData
        {
            public MessageData[] messages;
            public int total_count;
            public bool has_more;
        }

        private async System.Threading.Tasks.Task LoadHistoryPageAsync(bool reset)
        {
            if (_historyLoading || _noMoreHistory) return;
            _historyLoading = true;

            try
            {
                // 1) Выбираем lobbyId: сначала реальный из WS, иначе текущий, иначе "main"
                string lobbyId =
                    !string.IsNullOrEmpty(_lastLiveLobbyId)
                        ? _lastLiveLobbyId
                        : (LobbyVariables.Instance != null && LobbyVariables.Instance.currentLobby != null
                            ? LobbyVariables.Instance.currentLobby.lobbyId
                            : "main");

                // 2) Вспомогательная локальная функция запроса
                async System.Threading.Tasks.Task<(MessageData[] items, bool hasMore, long minId)> FetchAsync(
                    long? beforeId)
                {
                    string url = ApiRoutes.DOMAIN.TrimEnd('/') +
                                 "/api/client/messages?lobby_id=" + Uri.EscapeDataString(lobbyId) +
                                 "&limit=" + pageSize;

                    if (beforeId.HasValue)
                        url += "&before_id=" + beforeId.Value;

                    // лог — только в консоль, не в чат
                    Debug.Log($"[CHAT] GET {url}");

                    var req = new Proyecto26.RequestHelper
                    {
                        Uri = url,
                        Method = "GET",
                        Headers = ClientDataStorage.GetJwtHeader() // { Jwt: token }
                    };

                    Proyecto26.ResponseHelper resp;
                    try
                    {
                        resp = await Proyecto26.RestClient.Request(req).ToTask();
                    }
                    catch
                    {
                        // fallback: Bearer
                        req.Headers = new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "Authorization", "Bearer " + (ClientDataStorage.AccessToken ?? string.Empty) }
                        };
                        resp = await Proyecto26.RestClient.Request(req).ToTask();
                    }

                    if (resp.StatusCode >= 400)
                    {
                        Debug.LogWarning($"[CHAT] History HTTP {resp.StatusCode}");
                        return (null, false, long.MaxValue);
                    }

                    string text = resp.Text ?? string.Empty;
                    var env = JsonUtility.FromJson<HistoryEnvelopeDto>(text);
                    if (env?.success != true || env.data?.messages == null)
                    {
                        Debug.LogWarning($"[CHAT] History parse fail or no data. Raw: {text}");
                        return (null, false, long.MaxValue);
                    }

                    var arr = env.data.messages;
                    bool more = env.data.has_more;
                    long min = long.MaxValue;
                    for (int i = 0; i < arr.Length; i++)
                        if (arr[i].id > 0 && arr[i].id < min)
                            min = arr[i].id;

                    Debug.Log($"[CHAT] History items: {arr.Length}, has_more={more}, minId={min}");
                    return (arr, more, min);
                }

                // 3) Attempt A — обычный запрос
                long? beforeA =
                    (reset || _oldestMessageId == int.MaxValue) ? (long?)null : _oldestMessageId;

                var (itemsA, hasMoreA, minIdA) = await FetchAsync(beforeA);

                // Если пришло пусто, но у нас есть «живой» messageId — пробуем «якорить» по нему
                MessageData[] pageItems = itemsA;
                bool hasMore = hasMoreA;
                long minId = minIdA;

                if ((pageItems == null || pageItems.Length == 0) && _lastLiveMessageId > 0)
                {
                    // Attempt B — принудительный якорь по последнему живому сообщению
                    var (itemsB, hasMoreB, minIdB) = await FetchAsync(_lastLiveMessageId);
                    if (itemsB != null && itemsB.Length > 0)
                    {
                        pageItems = itemsB;
                        hasMore = hasMoreB;
                        minId = minIdB;
                    }
                }

                if (pageItems == null || pageItems.Length == 0)
                {
                    // Ничего не нашли — либо канал пуст, либо история не хранится
                    if (!hasMore) _noMoreHistory = true;
                    return;
                }

                // Сервер обычно отдаёт новые→старые; для prepend нужен порядок старые→новые
                Array.Reverse(pageItems);

                // Собираем батч и обновляем «якорь»
                var batch = new List<(string username, string message, UltimateChatBox.ChatStyle)>(pageItems.Length);
                for (int i = 0; i < pageItems.Length; i++)
                {
                    var m = pageItems[i];

                    string username =
                        (m.user != null && !string.IsNullOrEmpty(m.user.username))
                            ? m.user.username
                            : (m.user_id != 0 ? ("User#" + m.user_id) : "User");

                    var style = (m.type == "important")
                        ? UltimateChatBoxStyles.warningMessage
                        : UltimateChatBoxStyles.boldUsername;

                    batch.Add((username, m.message ?? string.Empty, style));
                }

                // Вставляем сверху без скачка
                try
                {
                    CurrentChatBox.PrependChats(batch);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CHAT] PrependChats error: {ex.Message}");
                }

                // Обновляем глобальный минимум для следующей страницы
                if (minId < _oldestMessageId) _oldestMessageId = (int)minId;

                // Если сервер сказал «страниц больше нет» — останавливаем автодогрузку
                if (!hasMore) _noMoreHistory = true;
            }
            finally
            {
                _historyLoading = false;
            }
        }
    }
}