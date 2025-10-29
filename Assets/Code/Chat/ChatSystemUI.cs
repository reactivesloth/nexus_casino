using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Chat
{
    /// <summary>
    /// Типы чата в системе
    /// </summary>
    public enum ChatType
    {
        Lobby,
        Global
    }

    /// <summary>
    /// Стиль отображения сообщения чата
    /// </summary>
    [Serializable]
    public class ChatMessageStyle
    {
        // Стили для username
        public bool usernameBold = false;
        public bool usernameItalic = false;
        public bool usernameUnderlined = false;
        public Color usernameColor = Color.white;
        
        // Стили для сообщения
        public bool messageBold = false;
        public bool messageItalic = false;
        public bool messageUnderlined = false;
        public Color messageColor = Color.white;
        
        // Дополнительные параметры
        public bool hideUsername = false;
        public bool noUsernameFollowup = false;

        /// <summary>
        /// Форматирует сообщение согласно стилю
        /// </summary>
        public string FormatMessage(string username, string message)
        {
            if (hideUsername)
            {
                return FormatText(message, messageBold, messageItalic, messageUnderlined, messageColor);
            }

            var formattedUsername =
                FormatText(username, usernameBold, usernameItalic, usernameUnderlined, usernameColor);
            var formattedMessage = FormatText(message, messageBold, messageItalic, messageUnderlined, messageColor);
            var followup = noUsernameFollowup ? " " : ": ";
            return string.IsNullOrEmpty(username)
                ? formattedMessage
                : $"{formattedUsername}{followup}{formattedMessage}";
        }

        private string FormatText(string text, bool bold, bool italic, bool underlined, Color color)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (bold) text = $"<b>{text}</b>";
            if (italic) text = $"<i>{text}</i>";
            if (underlined) text = $"<u>{text}</u>";
            if (color != Color.clear && color != Color.white)
                text = $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
            return text;
        }
    }

    /// <summary>
    /// Предустановленные стили сообщений
    /// </summary>
    public static class ChatStyles
    {
        public static readonly ChatMessageStyle Default = new ChatMessageStyle
        {
            usernameColor = Color.white,
            messageColor = Color.white
        };

        public static readonly ChatMessageStyle System = new ChatMessageStyle
        {
            usernameBold = true,
            usernameColor = new Color(1f, 1f, 0f), // Желтый
            messageColor = new Color(1f, 1f, 0f)
        };

        public static readonly ChatMessageStyle Error = new ChatMessageStyle
        {
            usernameBold = true,
            usernameColor = new Color(1f, 0.2f, 0.2f), // Красный
            messageColor = new Color(1f, 0.2f, 0.2f)
        };

        public static readonly ChatMessageStyle Warning = new ChatMessageStyle
        {
            usernameBold = true,
            usernameColor = new Color(1f, 0.6f, 0f), // Оранжевый
            messageColor = new Color(1f, 0.6f, 0f)
        };

        public static readonly ChatMessageStyle Notice = new ChatMessageStyle
        {
            usernameColor = new Color(0.4f, 0.8f, 1f), // Голубой
            messageColor = new Color(0.4f, 0.8f, 1f)
        };

        public static readonly ChatMessageStyle Important = new ChatMessageStyle
        {
            usernameBold = true,
            messageBold = true,
            usernameColor = new Color(1f, 0.84f, 0f), // Золотой
            messageColor = Color.white
        };

        public static readonly ChatMessageStyle BoldUsername = new ChatMessageStyle
        {
            usernameBold = true,
            usernameColor = Color.white,
            messageColor = Color.white
        };
    }

    /// <summary>
    /// Структура сообщения в чате
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        public string username;
        public string message;
        public long? messageId;
        public ChatType chatType;
        public DateTime timestamp;
        public ChatMessageStyle style;

        public ChatMessage()
        {
            timestamp = DateTime.Now;
            style = ChatStyles.Default;
        }
    }

    /// <summary>
    /// Главный UI компонент системы чата
    /// </summary>
    public class ChatSystemUI : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private GameObject chatPanel;
        
        [Header("UI References")] [SerializeField]
        private ScrollRect scrollRect;

        [SerializeField] private RectTransform contentParent;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI chatTypeIndicator;

        [Header("Chat Type Buttons")] [SerializeField]
        private Button localChatButton;

        [SerializeField] private Button globalChatButton;

        [Header("Send Button")] [SerializeField]
        private Button sendButton;
        
        [Header("Close Button")] [SerializeField]
        private Button closeButton;
        
        [Header("Prefabs")] [SerializeField] private GameObject messagePrefab;

        [Header("Settings")] [SerializeField] private int maxMessagesPerChat = 500;
        [SerializeField] private bool devLog = false;

        #endregion

        #region Private Fields

        // Хранение сообщений для каждого типа чата
        private Dictionary<ChatType, List<ChatMessage>> chatMessages =
            new Dictionary<ChatType, List<ChatMessage>>();

        private List<GameObject> activeMessageObjects = new List<GameObject>();
        private Queue<GameObject> messagePool = new Queue<GameObject>();

        // Состояние
        private ChatType currentChatType = ChatType.Lobby;
        private bool isVisible = false;
        private bool inputFieldActive = false;

        // История загрузки
        private Dictionary<ChatType, bool> historyLoading = new();
        private Dictionary<ChatType, bool> noMoreHistory = new();

        // Запоминаем, была ли прокрутка в самом низу для каждого типа чата
        private Dictionary<ChatType, bool> wasAtBottom = new();

        #endregion

        #region Events

        public event Action<ChatType> OnChatTypeChanged;
        public event Action<string> OnMessageSent;
        public event Action<string, string> OnCommandSent;
        public event Action OnInputFieldEnabled;
        public event Action OnInputFieldDisabled;
        public event Action<ChatType> OnReachedTop;

        #endregion

        #region Properties

        public ChatType CurrentChatType => currentChatType;
        public bool IsVisible => isVisible;
        public bool InputFieldActive => inputFieldActive;

        public string InputText
        {
            get => inputField != null ? inputField.text : string.Empty;
            set
            {
                if (inputField != null) inputField.text = value;
            }
        }

        public long? OldestMessageId
        {
            get
            {
                if (!chatMessages.ContainsKey(currentChatType) || chatMessages[currentChatType].Count == 0)
                    return null;
                return chatMessages[currentChatType].FirstOrDefault(m => m.messageId.HasValue)?.messageId;
            }
        }

        public bool NoMoreHistory
        {
            get => noMoreHistory.ContainsKey(currentChatType) && noMoreHistory[currentChatType];
            set => noMoreHistory[currentChatType] = value;
        }

        public bool IsAtBottom => scrollRect != null && scrollRect.verticalNormalizedPosition <= 0.02f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeChatData();
            SetupUI();
        }

        private void Update()
        {
            CheckScrollPosition();
        }

        private void OnDestroy()
        {
            UnsubscribeFromUIEvents();
        }

        #endregion

        #region Initialization

        private void InitializeChatData()
        {
            chatMessages[ChatType.Lobby] = new List<ChatMessage>();
            chatMessages[ChatType.Global] = new List<ChatMessage>();
            historyLoading[ChatType.Lobby] = false;
            historyLoading[ChatType.Global] = false;
            noMoreHistory[ChatType.Lobby] = false;
            noMoreHistory[ChatType.Global] = false;
            wasAtBottom[ChatType.Lobby] = true;
            wasAtBottom[ChatType.Global] = true;

            if (devLog) Debug.Log("[ChatSystemUI] Chat data initialized");
        }

        private void SetupUI()
        {
            // Подписка на события
            SubscribeToUIEvents();
            UpdateChatTypeIndicator();
            UpdateChatTypeButtons();
            Hide();

            if (devLog) Debug.Log("[ChatSystemUI] UI setup complete");
        }

        private void SubscribeToUIEvents()
        {
            if (inputField != null)
            {
                inputField.onSubmit.AddListener(OnInputSubmit);
                inputField.onSelect.AddListener((_) => OnInputFieldEnabled?.Invoke());
                inputField.onDeselect.AddListener((_) => OnInputFieldDisabled?.Invoke());
            }

            if (localChatButton != null)
            {
                localChatButton.onClick.AddListener(() => SetChatType(ChatType.Lobby));
            }

            if (globalChatButton != null)
            {
                globalChatButton.onClick.AddListener(() => SetChatType(ChatType.Global));
            }

            if (sendButton != null)
            {
                sendButton.onClick.AddListener(SendMessage);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }
            
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            }
        }

        private void UnsubscribeFromUIEvents()
        {
            if (inputField != null)
            {
                inputField.onSubmit.RemoveListener(OnInputSubmit);
            }

            if (localChatButton != null)
            {
                localChatButton.onClick.RemoveAllListeners();
            }

            if (globalChatButton != null)
            {
                globalChatButton.onClick.RemoveAllListeners();
            }

            if (sendButton != null)
            {
                sendButton.onClick.RemoveAllListeners();
            }
            
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
            }
            
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Показать чат
        /// </summary>
        public void Show()
        {
            if (isVisible) return;
            isVisible = true;
            chatPanel.SetActive(true);
            RefreshCurrentChat();
            StartCoroutine(EnableInputNextFrame());
        }

        private IEnumerator EnableInputNextFrame()
        {
            yield return null;
            EnableInputField();
        }

        /// <summary>
        /// Скрыть чат
        /// </summary>
        public void Hide()
        {
            if (!isVisible) return;
            isVisible = false;
            DisableInputField();
            chatPanel.SetActive(false);

            if (devLog) Debug.Log("[ChatSystemUI] Hide");
        }

        /// <summary>
        /// Активировать поле ввода
        /// </summary>
        public void EnableInputField()
        {
            if (inputFieldActive || inputField == null) return;
            inputFieldActive = true;
            inputField.interactable = true;
            inputField.Select();
            inputField.ActivateInputField();

            if (devLog) Debug.Log("[ChatSystemUI] Input field enabled");
        }

        /// <summary>
        /// Деактивировать поле ввода
        /// </summary>
        public void DisableInputField()
        {
            if (!inputFieldActive || inputField == null) return;
            inputFieldActive = false;
            inputField.interactable = false;
            inputField.text = "";
            inputField.DeactivateInputField();

            if (devLog) Debug.Log("[ChatSystemUI] Input field disabled");
        }

        /// <summary>
        /// Переключить тип чата
        /// </summary>
        public void SwitchChatType()
        {
            var newType = currentChatType == ChatType.Lobby ? ChatType.Global : ChatType.Lobby;
            SetChatType(newType);
        }

        /// <summary>
        /// Установить тип чата
        /// </summary>
        public void SetChatType(ChatType chatType)
        {
            if (currentChatType == chatType) return;

            // Сохраняем текущее состояние скролла перед переключением
            SaveScrollState();

            currentChatType = chatType;
            OnChatTypeChanged?.Invoke(currentChatType);
            UpdateChatTypeIndicator();
            UpdateChatTypeButtons();
            RefreshCurrentChat();

            if (devLog) Debug.Log($"[ChatSystemUI] Chat type changed to: {currentChatType}");
        }

        /// <summary>
        /// Отправить сообщение из поля ввода
        /// </summary>
        public void SendMessage()
        {
            if (inputField == null) return;
            var text = inputField.text;
            if (!string.IsNullOrEmpty(text))
            {
                OnInputSubmit(text);
            }
        }

        /// <summary>
        /// Добавить сообщение в чат
        /// </summary>
        public void AddMessage(string username, string message, ChatType chatType, ChatMessageStyle style = null,
            long? messageId = null)
        {
            var chatMessage = new ChatMessage
            {
                username = username,
                message = message,
                messageId = messageId,
                chatType = chatType,
                timestamp = DateTime.Now,
                style = style ?? ChatStyles.Default
            };

            // Добавляем сообщение в соответствующий чат
            if (!chatMessages.ContainsKey(chatType))
                chatMessages[chatType] = new List<ChatMessage>();

            chatMessages[chatType].Add(chatMessage);

            // Ограничиваем количество сообщений
            if (chatMessages[chatType].Count > maxMessagesPerChat)
            {
                chatMessages[chatType].RemoveAt(0);
            }

            // Обновляем UI только если это текущий активный чат
            if (chatType == currentChatType && isVisible)
            {
                // Проверяем, был ли пользователь внизу перед добавлением сообщения
                bool shouldScrollToBottom = IsAtBottom;
                
                CreateMessageUI(chatMessage);
                Canvas.ForceUpdateCanvases();
                
                // Прокручиваем вниз только если пользователь был внизу
                if (shouldScrollToBottom)
                {
                    StartCoroutine(ScrollToBottomNextFrame());
                }
            }

            if (devLog) Debug.Log($"[ChatSystemUI] Message added to {chatType}: {username}: {message}");
        }

        /// <summary>
        /// Добавить сообщения в начало истории
        /// </summary>
        public void PrependMessages(List<(string username, string message, ChatMessageStyle style, long id)> messages,
            ChatType chatType)
        {
            if (messages == null || messages.Count == 0) return;
            if (!chatMessages.ContainsKey(chatType))
                chatMessages[chatType] = new List<ChatMessage>();

            var chatMessageList = new List<ChatMessage>();
            foreach (var (username, message, style, id) in messages)
            {
                chatMessageList.Add(new ChatMessage
                {
                    username = username,
                    message = message,
                    messageId = id,
                    chatType = chatType,
                    timestamp = DateTime.Now,
                    style = style ?? ChatStyles.Default
                });
            }

            // Вставляем в начало списка
            chatMessages[chatType].InsertRange(0, chatMessageList);

            // Ограничиваем количество сообщений
            if (chatMessages[chatType].Count > maxMessagesPerChat)
            {
                var excess = chatMessages[chatType].Count - maxMessagesPerChat;
                chatMessages[chatType].RemoveRange(maxMessagesPerChat, excess);
            }

            // Обновляем UI только если это текущий активный чат
            if (chatType == currentChatType && isVisible)
            {
                var scrollPosBefore = scrollRect.verticalNormalizedPosition;
                RefreshCurrentChat();
                scrollRect.verticalNormalizedPosition = scrollPosBefore;
            }

            if (devLog) Debug.Log($"[ChatSystemUI] Prepended {messages.Count} messages to {chatType}");
        }

        /// <summary>
        /// Прокрутить вверх
        /// </summary>
        public void ScrollUp(int lines = 1)
        {
            if (scrollRect == null) return;
            var scrollValue = scrollRect.verticalNormalizedPosition + (lines * 0.1f);
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollValue);
            
            // Обновляем состояние
            SaveScrollState();
        }

        /// <summary>
        /// Прокрутить вниз
        /// </summary>
        public void ScrollDown(int lines = 1)
        {
            if (scrollRect == null) return;
            var scrollValue = scrollRect.verticalNormalizedPosition - (lines * 0.1f);
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollValue);
            
            // Обновляем состояние
            SaveScrollState();
        }

        /// <summary>
        /// Прокрутить в начало
        /// </summary>
        public void ScrollToTop()
        {
            if (scrollRect == null) return;
            scrollRect.verticalNormalizedPosition = 1f;
            SaveScrollState();
        }

        /// <summary>
        /// Прокрутить в конец
        /// </summary>
        public void ScrollToBottom()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
            SaveScrollState();
        }

        /// <summary>
        /// Очистить чат
        /// </summary>
        public void ClearChat(ChatType chatType)
        {
            if (chatMessages.ContainsKey(chatType))
            {
                chatMessages[chatType].Clear();
            }

            if (chatType == currentChatType && isVisible)
            {
                RefreshCurrentChat();
            }

            if (devLog) Debug.Log($"[ChatSystemUI] Chat cleared: {chatType}");
        }

        #endregion

        #region Private Methods

        private void RefreshCurrentChat()
        {
            ClearActiveMessages();

            if (!chatMessages.ContainsKey(currentChatType))
                return;

            var messages = chatMessages[currentChatType];
            foreach (var message in messages)
            {
                CreateMessageUI(message);
            }

            Canvas.ForceUpdateCanvases();
            
            // Восстанавливаем позицию скролла
            RestoreScrollState();
        }

        private void CreateMessageUI(ChatMessage message)
        {
            var messageObj = GetMessageObject();
            if (messageObj == null) return;

            messageObj.transform.SetParent(contentParent, false);
            messageObj.SetActive(true);

            // Настройка текста сообщения
            var textComponent = messageObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                var formattedMessage = message.style.FormatMessage(message.username, message.message);
                textComponent.text = formattedMessage;
            }

            activeMessageObjects.Add(messageObj);
        }

        private GameObject GetMessageObject()
        {
            if (messagePool.Count > 0)
            {
                return messagePool.Dequeue();
            }

            if (messagePrefab != null)
            {
                return Instantiate(messagePrefab);
            }

            Debug.LogError("[ChatSystemUI] No message prefab assigned!");
            return null;
        }

        private void ReturnMessageObject(GameObject messageObj)
        {
            if (messageObj == null) return;
            messageObj.SetActive(false);
            messageObj.transform.SetParent(transform, false);
            messagePool.Enqueue(messageObj);
        }

        private void ClearActiveMessages()
        {
            foreach (var messageObj in activeMessageObjects)
            {
                ReturnMessageObject(messageObj);
            }

            activeMessageObjects.Clear();
        }

        private void UpdateChatTypeIndicator()
        {
            if (chatTypeIndicator != null)
            {
                 //chatTypeIndicator.text = currentChatType == ChatType.Lobby ? "LOBBY" : "GLOBAL";
            }
        }

        private void UpdateChatTypeButtons()
        {
            // Подсвечиваем активную кнопку типа чата
            if (localChatButton != null)
            {
                localChatButton.GetComponent<Outline>().enabled = currentChatType == ChatType.Lobby;
            }

            if (globalChatButton != null)
            {
                globalChatButton.GetComponent<Outline>().enabled = currentChatType == ChatType.Global;
            }
        }

        private void SaveScrollState()
        {
            if (scrollRect == null) return;
            wasAtBottom[currentChatType] = IsAtBottom;
        }

        private void RestoreScrollState()
        {
            if (scrollRect == null) return;
            
            // Если пользователь был внизу, прокручиваем вниз
            if (wasAtBottom.ContainsKey(currentChatType) && wasAtBottom[currentChatType])
            {
                StartCoroutine(ScrollToBottomNextFrame());
            }
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null; // Ждем один кадр
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void OnInputSubmit(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Проверяем на команду
            if (text.StartsWith("/"))
            {
                ParseCommand(text);
            }
            else
            {
                OnMessageSent?.Invoke(text);
            }

            inputField.text = "";
            inputField.ActivateInputField();
        }

        private void ParseCommand(string text)
        {
            var parts = text.Split(new[] {' '}, 2);
            var command = parts[0].Substring(1); // Убираем '/'
            var message = parts.Length > 1 ? parts[1] : "";
            OnCommandSent?.Invoke(command, message);
        }

        private void CheckScrollPosition()
        {
            if (scrollRect == null || !isVisible) return;

            // Проверяем достижение верха
            if (scrollRect.verticalNormalizedPosition >= 0.95f)
            {
                if (!historyLoading[currentChatType] && !noMoreHistory[currentChatType])
                {
                    historyLoading[currentChatType] = true;
                    OnReachedTop?.Invoke(currentChatType);
                }
            }
            else
            {
                historyLoading[currentChatType] = false;
            }
        }

        private void OnScrollValueChanged(Vector2 scrollValue)
        {
            // Сохраняем состояние при изменении скролла
            SaveScrollState();
        }

        #endregion
    }
}