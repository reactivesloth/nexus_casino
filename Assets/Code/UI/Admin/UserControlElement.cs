using System;
using Code.Chat;
using Code.Network.Lobby;
using Code.Network.Lobby.Data;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class UserControlElement: ControlElement
    {
        [SerializeField] private TMP_Text roleText;
        
        [SerializeField] private Button kickButton;
        
        [SerializeField] private Button banButton;
        
        [Space, SerializeField] private Button muteChatButton;
        [SerializeField] private Button unmuteChatButton;
        
        [Space, SerializeField] private Button muteVoiceButton;
        [SerializeField] private Button unmuteVoiceButton;

        private AdminPanelHandler _adminPanelHandler;
        private LobbyData.LobbyMember _lobbyMemberData;

        private string Username => _lobbyMemberData.displayName;

        private void Awake()
        {
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            _adminPanelHandler.MutedDictionary.OnChange += OnMutedDictionaryChange;
            
            kickButton.onClick.AddListener(OnKickClicked);
            banButton.onClick.AddListener(OnBanClicked);
            muteChatButton.onClick.AddListener(OnMuteChatClicked);
            unmuteChatButton.onClick.AddListener(OnUnmuteChatClicked);
            muteVoiceButton.onClick.AddListener(OnMuteVoiceClicked);
            unmuteVoiceButton.onClick.AddListener(OnUnmuteVoiceClicked);
        }
        
        private void OnDisable()
        {
            _adminPanelHandler.MutedDictionary.OnChange -= OnMutedDictionaryChange;
            
            kickButton.onClick.RemoveListener(OnKickClicked);
            banButton.onClick.RemoveListener(OnBanClicked);
            muteChatButton.onClick.RemoveListener(OnMuteChatClicked);
            unmuteChatButton.onClick.RemoveListener(OnUnmuteChatClicked);
            muteVoiceButton.onClick.RemoveListener(OnMuteVoiceClicked);
            unmuteVoiceButton.onClick.RemoveListener(OnUnmuteVoiceClicked);
        }
        
        
        private void OnMutedDictionaryChange(SyncDictionaryOperation operation, string key, AdminPanelHandler.MuteStateSync value, bool asServer)
        {
            if (key == Username)
                SetMutedButtonsState(value.muteChat, value.muteVoice);
        }


        public void Init(LobbyData.LobbyMember lobbyMemberData)
        {
            _lobbyMemberData = lobbyMemberData;
            
            titleDisplayText.text = _lobbyMemberData.displayName;
            roleText.text = _lobbyMemberData.Attributes.TryGetValue(LobbyController.Role, out var role) ? role : string.Empty;
            
            if (_adminPanelHandler.MutedDictionary.TryGetValue(Username, out var muteState))
                SetMutedButtonsState(muteState.muteChat, muteState.muteVoice);
            else
                SetMutedButtonsState(false, false);
            
            SearchKey = lobbyMemberData.displayName;
        }

        private void OnKickClicked()
        {
            // TODO: Kick Popup
            _adminPanelHandler.Kick(Username);
        }

        private void OnBanClicked()
        {
            // TODO: Ban Popup with time input
            _adminPanelHandler.BanUser(Username, 0);
        }

        private void OnMuteChatClicked()
        {
            _adminPanelHandler.MuteChat(Username);
        }

        private void OnUnmuteChatClicked()
        {
            _adminPanelHandler.UnmuteChat(Username);
        }

        private void OnMuteVoiceClicked()
        {
            _adminPanelHandler.MuteVoice(Username);
        }

        private void OnUnmuteVoiceClicked()
        {
            _adminPanelHandler.UnmuteVoice(Username);
        }

        private void SetMutedButtonsState(bool isMuteChat, bool isUnmuteVoice)
        {
            muteChatButton.gameObject.SetActive(!isMuteChat);
            unmuteChatButton.gameObject.SetActive(isMuteChat);
            
            muteVoiceButton.gameObject.SetActive(!isUnmuteVoice);
            unmuteVoiceButton.gameObject.SetActive(isUnmuteVoice);
        }
    }
}