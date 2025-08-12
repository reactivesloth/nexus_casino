using System;
using System.Collections.Generic;
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
        [SerializeField] private string systemName = "[SYSTEM]";
        [SerializeField] private UltimateChatBox lobbyChatBox;
        [SerializeField] private UltimateChatBox globalChatBox;
        [SerializeField] private List<CommandData> commands;

        [Header("ChatPosition settings")]
        [SerializeField] private Vector2 desktopPosition;
        [SerializeField] private Vector2 mobilePosition;

        public readonly Dictionary<string, CommandData> CommandsDictionary = new();

        private WebSocket _ws;
        private float _pingInterval = 5f;
        private float _pingTimer;
        private bool _isGlobalChatActive;

        public UltimateChatBox CurrentChatBox { get; private set; }
        public string SystemName => systemName;

        private void Start()
        {
            // построение словаря без LINQ/ForEach
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

            SetCurrentChat(lobbyChatBox);
            CurrentChatBox.Disable();
            CurrentChatBox.DisableInputField();

            // безопасная сборка URL
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
                try { _ws.Close(); } catch { /* ignore */ }
                _ws = null;
            }

            // отписка от UI-событий текущего бокса
            if (CurrentChatBox != null)
            {
                CurrentChatBox.OnExtraImageInteract -= SendMessage;
                CurrentChatBox.OnInputFieldEnabled -= OnInputFieldEnabled;
                CurrentChatBox.OnInputFieldDisabled -= OnInputFieldDisabled;
                CurrentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
                CurrentChatBox.OnInputFieldCommandSubmitted -= OnInputFieldCommandSubmitted;
                CurrentChatBox.OnInputFieldUpdated -= OnInputFieldUpdated;
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
                try { _ws.SendText("{\"event\":\"ping\",\"data\":{}}"); } catch { }
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
            // ре-коннект только при ненормальном закрытии
            if (code != WebSocketCloseCode.Normal)
            {
                try { _ws?.Connect(); } catch { }
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
            try { text = System.Text.Encoding.UTF8.GetString(data); }
            catch { return; }

            var envelope = JsonUtility.FromJson<ChatModel<Empty>>(text);
            if (envelope == null || string.IsNullOrEmpty(envelope.@event)) return;

            if (envelope.@event == ChatSocketEvents.NewMessage || envelope.@event == ChatSocketEvents.NewImportantMessage)
            {
                var msg = JsonUtility.FromJson<ChatModel<NewMessageData>>(text);
                if (msg == null || msg.data == null || msg.data.message == null) return;

                var m = msg.data.message;
                var lobby = LobbyVariables.Instance != null ? LobbyVariables.Instance.currentLobby : null;

                if (lobby != null && m.lobby_id == lobby.lobbyId)
                    HandleLobbyMassage(m);

                HandleGlobalMassage(m);
            }
            else if (envelope.@event == ChatSocketEvents.Error)
            {
                var err = JsonUtility.FromJson<ChatModel<Error>>(text);
                if (err != null && err.data != null)
                    SendSystemMessage(err.data.message, UltimateChatBoxStyles.errorMessage);
            }
            // игнор остальных событий
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

            if (CurrentChatBox != null)
            {
                CurrentChatBox.OnExtraImageInteract -= SendMessage;
                CurrentChatBox.OnInputFieldEnabled -= OnInputFieldEnabled;
                CurrentChatBox.OnInputFieldDisabled -= OnInputFieldDisabled;
                CurrentChatBox.OnInputFieldSubmitted -= OnInputFieldSubmittedCurrentBox;
                CurrentChatBox.OnInputFieldCommandSubmitted -= OnInputFieldCommandSubmitted;
                CurrentChatBox.OnInputFieldUpdated -= OnInputFieldUpdated;
                CurrentChatBox.Disable();
            }

            CurrentChatBox = chatBox;
            _isGlobalChatActive = (CurrentChatBox == globalChatBox);

            if (globalChatBox != null) globalChatBox.gameObject.SetActive(CurrentChatBox == globalChatBox);
            if (lobbyChatBox  != null) lobbyChatBox.gameObject.SetActive(CurrentChatBox == lobbyChatBox);

            CurrentChatBox.OnExtraImageInteract += SendMessage;
            CurrentChatBox.OnInputFieldEnabled += OnInputFieldEnabled;
            CurrentChatBox.OnInputFieldDisabled += OnInputFieldDisabled;
            CurrentChatBox.OnInputFieldSubmitted += OnInputFieldSubmittedCurrentBox;
            CurrentChatBox.OnInputFieldCommandSubmitted += OnInputFieldCommandSubmitted;
            CurrentChatBox.OnInputFieldUpdated += OnInputFieldUpdated;

            CurrentChatBox.EnableInputField();
            CurrentChatBox.Enable();
        }

        private void OnInputFieldUpdated(string _)
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

        private void OnInputFieldCommandSubmitted(string command, string message)
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
            try { _ws?.SendText(json); } catch { }
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
            if (lobbyChatBox != null)  lobbyChatBox.RegisterChat(systemName, msg, style);
            if (globalChatBox != null) globalChatBox.RegisterChat(systemName, msg, style);
        }
    }
}
