using System;
using Code.Chat;
using Code.Network.Lobby;
using Code.Network.Lobby.Data;
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
            kickButton.onClick.AddListener(OnKickClicked);
            banButton.onClick.AddListener(OnBanClicked);
            muteChatButton.onClick.AddListener(OnMuteChatClicked);
            unmuteChatButton.onClick.AddListener(OnUnmuteChatClicked);
            muteVoiceButton.onClick.AddListener(OnMuteVoiceClicked);
            unmuteVoiceButton.onClick.AddListener(OnUnmuteVoiceClicked);
        }

        private void OnDisable()
        {
            kickButton.onClick.RemoveListener(OnKickClicked);
            banButton.onClick.RemoveListener(OnBanClicked);
            muteChatButton.onClick.RemoveListener(OnMuteChatClicked);
            unmuteChatButton.onClick.RemoveListener(OnUnmuteChatClicked);
            muteVoiceButton.onClick.RemoveListener(OnMuteVoiceClicked);
            unmuteVoiceButton.onClick.RemoveListener(OnUnmuteVoiceClicked);
        }

        public void Init(LobbyData.LobbyMember lobbyMemberData)
        {
            _lobbyMemberData = lobbyMemberData;
            
            titleDisplayText.text = _lobbyMemberData.displayName;
            roleText.text = _lobbyMemberData.Attributes.TryGetValue(LobbyController.Role, out var role) ? role : string.Empty;
            
            SearchKey = lobbyMemberData.displayName;
        }

        private void OnKickClicked()
        {
            _adminPanelHandler.Kick(Username);
        }

        private void OnBanClicked()
        {
            _adminPanelHandler.BanUser(Username);
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
    }
}