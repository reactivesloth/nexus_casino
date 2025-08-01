using System;
using System.Collections.Generic;
using System.Linq;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using Code.Player;
using Code.Utility;
using NativeWebSocket;
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
            chatWebSocket = new WebSocket("wss://wss.ru", ClientDataStorage.GetJwtHeader()); 
            //chatWebSocket.Connect();
            chatWebSocket.OnMessage += OnMessageRecived;
        }

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.Tab) && CurrentChatBox.IsEnabled)
                ChangeChat();
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

            var style = UltimateChatBoxStyles.none;
            
            HandleGlobalMassage(messageData, style);
            if(messageData.lobby == LobbyVariables.Instance.currentLobby.lobbyId)
                HandleLobbyMassage(messageData, style);
        }

        private void OnInputFieldSubmittedCurrentBox(string text)
        {
            if (CurrentChatBox.InputFieldContainsCommand)
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
        
        public void HandleGlobalMassage(MessageData message, UltimateChatBox.ChatStyle style = null)
        {
            globalChatBox.RegisterChat($"[...{message.lobby.Substring(message.lobby.Length - 4)}]{message.username}", message.text, style);
        }

        public void HandleLobbyMassage(MessageData message, UltimateChatBox.ChatStyle style = null)
        {
            lobbyChatBox.RegisterChat($"{message.username}", message.text, style);
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