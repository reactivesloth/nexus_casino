using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.API.Models;
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
        /// Строит весь форматированный текст сообщения
        /// </summary>
        public string FormatMessage(string username, string message)
        {
            if (hideUsername)
                return FormatMessageText(message);

            var formattedUsername = FormatUsername(username);
            var formattedMessage = FormatMessageText(message);
            var followup = noUsernameFollowup ? " " : ": ";
            return string.IsNullOrEmpty(username)
                ? formattedMessage
                : $"{formattedUsername}{followup}{formattedMessage}";
        }

        /// <summary>
        /// Форматирует только никнейм
        /// </summary>
        public string FormatUsername(string username)
        {
            return FormatText(username, usernameBold, usernameItalic, usernameUnderlined, usernameColor);
        }

        /// <summary>
        /// Форматирует только текст сообщения
        /// </summary>
        public string FormatMessageText(string message)
        {
            return FormatText(message, messageBold, messageItalic, messageUnderlined, messageColor);
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

        [Header("Chat Type Buttons")] [SerializeField]
        private Button localChatButton;

        [SerializeField] private Button globalChatButton;

        [Header("Send Button")] [SerializeField]
        private Button sendButton;

        [Header("Close Button")] [SerializeField]
        private Button closeButton;

        [Header("Prefabs")] [SerializeField] private MessageComponent messagePrefab;

        [Header("Settings")] [SerializeField] private int maxMessagesPerChat = 500;
        [SerializeField] private float timeToHoldAtTop = 1f;
        [SerializeField] private bool devLog = false;

        #endregion

        #region Private Fields

        // Хранение сообщений для каждого типа чата
        private readonly Dictionary<ChatType, List<ChatMessage>> _chatMessages = new();
        private readonly List<ChatMessage> _allMessages = new();

        private List<MessageComponent> _activeMessageObjects = new();
        private readonly Dictionary<long, MessageComponent> _activeUsersMessageObjects = new();
        private readonly Queue<MessageComponent> _messagePool = new();

        // Состояние
        private ChatType _currentChatType = ChatType.Lobby;
        private bool _isVisible = false;
        private bool _inputFieldActive = false;

        // История загрузки
        private readonly Dictionary<ChatType, bool> _historyLoading = new();
        private readonly Dictionary<ChatType, bool> _noMoreHistory = new();

        // Запоминаем, была ли прокрутка в самом низу для каждого типа чата
        private readonly Dictionary<ChatType, bool> _wasAtBottom = new();
        
        private float _timeAtTop = 0f;
        private bool _hasTriggeredOnReachedTop = false;

        #endregion

        #region Events

        public event Action<ChatType> OnChatTypeChanged;
        public event Action<string> OnMessageSent;
        public event Action<string, string> OnCommandSent;
        public event Action OnInputFieldEnabled;
        public event Action OnInputFieldDisabled;
        public event Action<ChatType> OnReachedTop;

        #endregion

        #region Propertiesй

        public ChatType CurrentChatType => _currentChatType;
        public bool IsVisible => _isVisible;
        public bool InputFieldActive => _inputFieldActive;

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
                if (!_chatMessages.ContainsKey(_currentChatType) || _chatMessages[_currentChatType].Count == 0)
                    return null;
                return _chatMessages[_currentChatType].FirstOrDefault(m => m.chatMessageData != null)?.chatMessageData
                    ?.id;
            }
        }

        public bool NoMoreHistory
        {
            get => _noMoreHistory.ContainsKey(_currentChatType) && _noMoreHistory[_currentChatType];
            set => _noMoreHistory[_currentChatType] = value;
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
            _chatMessages[ChatType.Lobby] = new List<ChatMessage>();
            _chatMessages[ChatType.Global] = new List<ChatMessage>();
            _historyLoading[ChatType.Lobby] = false;
            _historyLoading[ChatType.Global] = false;
            _noMoreHistory[ChatType.Lobby] = false;
            _noMoreHistory[ChatType.Global] = false;
            _wasAtBottom[ChatType.Lobby] = true;
            _wasAtBottom[ChatType.Global] = true;

            if (devLog) Debug.Log("[ChatSystemUI] Chat data initialized");
        }

        private void SetupUI()
        {
            // Подписка на события
            SubscribeToUIEvents();
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
            if (_isVisible) return;
            _isVisible = true;
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
            if (!_isVisible) return;
            _isVisible = false;
            DisableInputField();
            chatPanel.SetActive(false);

            if (devLog) Debug.Log("[ChatSystemUI] Hide");
        }

        /// <summary>
        /// Активировать поле ввода
        /// </summary>
        public void EnableInputField()
        {
            if (_inputFieldActive || inputField == null) return;
            _inputFieldActive = true;
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
            if (!_inputFieldActive || inputField == null) return;
            _inputFieldActive = false;
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
            var newType = _currentChatType == ChatType.Lobby ? ChatType.Global : ChatType.Lobby;
            SetChatType(newType);
        }

        /// <summary>
        /// Установить тип чата
        /// </summary>
        public void SetChatType(ChatType chatType)
        {
            if (_currentChatType == chatType) return;

            // Сохраняем текущее состояние скролла перед переключением
            SaveScrollState();

            _currentChatType = chatType;
            OnChatTypeChanged?.Invoke(_currentChatType);
            UpdateChatTypeButtons();
            RefreshCurrentChat();

            if (devLog) Debug.Log($"[ChatSystemUI] Chat type changed to: {_currentChatType}");
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
        public void AddMessage(ChatMessage message)
        {
            // Добавляем сообщение в соответствующий чат
            if (!_chatMessages.ContainsKey(message.chatType))
                _chatMessages[message.chatType] = new List<ChatMessage>();

            _chatMessages[message.chatType].Add(message);
            _allMessages.Add(message);

            // Ограничиваем количество сообщений
            if (_chatMessages[message.chatType].Count > maxMessagesPerChat)
            {
                var messageForDelete = _chatMessages[message.chatType][0];
                _allMessages.Remove(messageForDelete);
                _chatMessages[message.chatType].Remove(messageForDelete);
            }

            // Обновляем UI только если это текущий активный чат
            if (message.chatType == _currentChatType && _isVisible)
            {
                // Проверяем, был ли пользователь внизу перед добавлением сообщения
                bool shouldScrollToBottom = IsAtBottom;

                CreateMessageUI(message);
                Canvas.ForceUpdateCanvases();

                // Прокручиваем вниз только если пользователь был внизу
                if (shouldScrollToBottom)
                {
                    StartCoroutine(ScrollToBottomNextFrame());
                }
            }

            if (devLog)
                Debug.Log(
                    $"[ChatSystemUI] Message added to {message.chatType}: {message.displayUsername}: {message.displayMessage}");
        }

        public void OnLikeUpdated(LikeUpdatedModel likeUpdatedData)
        {
            if (_activeUsersMessageObjects.TryGetValue(likeUpdatedData.message_id, out var messagesComponent))
                messagesComponent.UpdateLikesStatus(likeUpdatedData.likes_count, likeUpdatedData.is_liked_by_me);
            foreach (var messageForUpdate in _allMessages.Where(
                         m => m.chatMessageData?.id == likeUpdatedData.message_id))
            {
                if(messageForUpdate.chatMessageData == null)
                    continue;
                messageForUpdate.chatMessageData.likes_count = likeUpdatedData.likes_count;
                messageForUpdate.chatMessageData.is_liked_by_me = likeUpdatedData.is_liked_by_me;
            }
        }

        public void OnViewUpdated(ViewUpdatedModel viewUpdatedData)
        {
            if (_activeUsersMessageObjects.TryGetValue(viewUpdatedData.message_id, out var messagesComponent))
                messagesComponent.UpdateViewsStatus(viewUpdatedData.views_count, viewUpdatedData.is_viewed_by_me);
            foreach (var messageForUpdate in _allMessages.Where(
                         m => m.chatMessageData?.id == viewUpdatedData.message_id))
            {
                if(messageForUpdate.chatMessageData == null)
                    continue;
                messageForUpdate.chatMessageData.views_count = viewUpdatedData.views_count;
                messageForUpdate.chatMessageData.is_viewed_by_me = viewUpdatedData.is_viewed_by_me;
            }
        }

        /// <summary>
        /// Добавить сообщения в начало истории
        /// </summary>
        public void PrependMessages(List<ChatMessage> messages, ChatType chatType)
        {
            if (messages == null || messages.Count == 0) return;
            if (!_chatMessages.ContainsKey(chatType))
                _chatMessages[chatType] = new List<ChatMessage>();

            var chatMessageList = new List<ChatMessage>();
            foreach (var message in messages)
            {
                chatMessageList.Add(message);
            }

            // Вставляем в начало списка
            _chatMessages[chatType].InsertRange(0, chatMessageList);
            _allMessages.InsertRange(0, chatMessageList);

            // Ограничиваем количество сообщений
            if (_chatMessages[chatType].Count > maxMessagesPerChat)
            {
                var excess = _chatMessages[chatType].Count - maxMessagesPerChat;
                var messagesForRemove = _chatMessages[chatType].GetRange(maxMessagesPerChat, excess);
                messagesForRemove.ForEach(m =>
                {
                    _chatMessages[chatType].Remove(m);
                    _allMessages.Remove(m);
                });
            }

            // Обновляем UI только если это текущий активный чат
            if (chatType == _currentChatType && _isVisible)
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
            if (_chatMessages.ContainsKey(chatType))
            {
                _chatMessages[chatType].Clear();
            }

            _allMessages.Clear();

            if (chatType == _currentChatType && _isVisible)
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

            if (!_chatMessages.ContainsKey(_currentChatType))
                return;

            var messages = _chatMessages[_currentChatType];
            foreach (var message in messages)
            {
                CreateMessageUI(message);
            }

            Canvas.ForceUpdateCanvases();

            // Восстанавливаем позицию скролла
            RestoreScrollState();
            Debug.Log(_activeUsersMessageObjects.Count);
        }

        private void CreateMessageUI(ChatMessage message)
        {
            var messageComponent = GetMessageObject();
            if (messageComponent == null) return;

            messageComponent.transform.SetParent(contentParent, false);
            messageComponent.gameObject.SetActive(true);

            // Настройка текста сообщения
            messageComponent.Init(message);

            _activeMessageObjects.Add(messageComponent);
            if (message.chatMessageData != null)
                _activeUsersMessageObjects.TryAdd(message.chatMessageData.id, messageComponent);
        }

        private MessageComponent GetMessageObject()
        {
            if (_messagePool.Count > 0)
            {
                return _messagePool.Dequeue();
            }

            if (messagePrefab != null)
            {
                return Instantiate(messagePrefab);
            }

            Debug.LogError("[ChatSystemUI] No message prefab assigned!");
            return null;
        }

        private void ReturnMessageObject(MessageComponent messageObj)
        {
            if (messageObj == null) return;
            messageObj.gameObject.SetActive(false);
            messageObj.transform.SetParent(transform, false);
            _messagePool.Enqueue(messageObj);
        }

        private void ClearActiveMessages()
        {
            foreach (var messageObj in _activeMessageObjects)
            {
                ReturnMessageObject(messageObj);
            }

            _activeMessageObjects.Clear();
            _activeUsersMessageObjects.Clear();
        }

        private void UpdateChatTypeButtons()
        {
            if (localChatButton != null)
                localChatButton.GetComponent<Outline>().enabled = _currentChatType == ChatType.Lobby;
            if (globalChatButton != null)
                globalChatButton.GetComponent<Outline>().enabled = _currentChatType == ChatType.Global;
        }

        private void SaveScrollState()
        {
            if (scrollRect == null) return;
            _wasAtBottom[_currentChatType] = IsAtBottom;
        }

        private void RestoreScrollState()
        {
            if (scrollRect == null) return;

            // Если пользователь был внизу, прокручиваем вниз
            if (_wasAtBottom.ContainsKey(_currentChatType) && _wasAtBottom[_currentChatType])
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
            var parts = text.Split(new[] { ' ' }, 2);
            var command = parts[0].Substring(1); // Убираем '/'
            var message = parts.Length > 1 ? parts[1] : "";
            OnCommandSent?.Invoke(command, message);
        }

        private void CheckScrollPosition()
        {
            if (scrollRect == null || !_isVisible)
                return;

            if (scrollRect.verticalNormalizedPosition >= 0.95f)
            {
                // Скролл у верха, накапливаем время
                _timeAtTop += Time.deltaTime;

                if (_timeAtTop >= timeToHoldAtTop && !_hasTriggeredOnReachedTop)
                {
                    // Время достигнуто, вызываем событие
                    OnReachedTop?.Invoke(_currentChatType);
                    _hasTriggeredOnReachedTop = true;
                }
            }
            else
            {
                // Скролл не у верха, сбрасываем время и флаг
                _timeAtTop = 0f;
                _hasTriggeredOnReachedTop = false;
            }

            // Далее ваша старая логика загрузки истории, если нужна, можно дополнить
            // Например, если хотите, чтобы загрузка продолжалась как раньше:
            if (!_historyLoading[_currentChatType] && !_noMoreHistory[_currentChatType])
            {
                _historyLoading[_currentChatType] = true;
                // Загрузка истории
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