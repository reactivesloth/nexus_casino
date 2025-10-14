using System;
using Code.Chat;
using Code.Network.Lobby;
using Code.Network.Lobby.Data;
using Code.UI.Popup;
using FishNet.Object.Synchronizing;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class UserControlElement : ControlElement
    {
        [SerializeField] private TMP_Text roleText;

        [SerializeField] private Button kickButton;

        [SerializeField] private Button banButton;

        [Space, SerializeField] private Button muteChatButton;
        [SerializeField] private Button unmuteChatButton;

        [Space, SerializeField] private Button muteVoiceButton;
        [SerializeField] private Button unmuteVoiceButton;
        
        [SerializeField] private Button promoteButton;

        private AdminPanelHandler _adminPanelHandler;
        private LobbyData.LobbyMember _lobbyMemberData;
        private NexusModularPopupOpener _popupOpener;

        private string Username => _lobbyMemberData.displayName;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
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
            
            promoteButton.onClick.AddListener(OnPromoteButtonClicked);
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
            
            promoteButton.onClick.RemoveListener(OnPromoteButtonClicked);
        }


        private void OnMutedDictionaryChange(SyncDictionaryOperation operation, string key,
            AdminPanelHandler.MuteStateSync value, bool asServer)
        {
            if (key == Username)
                SetMutedButtonsState(value.muteChat, value.muteVoice);
        }


        public void Init(LobbyData.LobbyMember lobbyMemberData)
        {
            _lobbyMemberData = lobbyMemberData;

            titleDisplayText.text = _lobbyMemberData.displayName;
            roleText.text = _lobbyMemberData.Attributes.TryGetValue(LobbyController.Role, out var role)
                ? role
                : string.Empty;

            if (_adminPanelHandler.MutedDictionary.TryGetValue(Username, out var muteState))
                SetMutedButtonsState(muteState.muteChat, muteState.muteVoice);
            else
                SetMutedButtonsState(false, false);

            SearchKey = lobbyMemberData.displayName;
        }

        private void OnKickClicked()
        {
            _popupOpener.Title = "Kick";
            _popupOpener.Subtitle = $"Do You want kick {Username}?";

            var yesButtonInfo = new ButtonInfo
            {
                ClosePopupWhenClicked = true,
                Label = "Yes",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            yesButtonInfo.OnClickedEvent.AddListener(KickAction);
            _popupOpener.Buttons.Add(yesButtonInfo);

            var cancelButtonInfo = new ButtonInfo
            {
                ClosePopupWhenClicked = true,
                IgnoreButtonClickedEvent = true,
                Label = "Cancel"
            };
            _popupOpener.Buttons.Add(cancelButtonInfo);

            _popupOpener.OpenPopup();
        }

        private void KickAction() => _adminPanelHandler.Kick(Username);

        private void OnBanClicked()
        {
            _popupOpener.Title = "Ban";
            _popupOpener.Subtitle = $"Do You want ban {Username}?";

            _popupOpener.Inputs.Add(new InputInfo
            {
                type = InputInfoType.InputField,
                labelName = "Time in minutes",
                contentType = TMP_InputField.ContentType.IntegerNumber
            });

            var yesButtonInfo = new ButtonInfo
            {
                ClosePopupWhenClicked = true,
                Label = "Yes",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            _popupOpener.Buttons.Add(yesButtonInfo);

            var cancelButtonInfo = new ButtonInfo
            {
                ClosePopupWhenClicked = true,
                IgnoreButtonClickedEvent = true,
                Label = "Cancel"
            };
            _popupOpener.Buttons.Add(cancelButtonInfo);

            _popupOpener.OpenPopup();

            yesButtonInfo.OnClickedEvent.AddListener(
                () => BanAction(int.Parse(_popupOpener.LastPopup.GetInputValue(0))));
        }

        private void BanAction(int time) => _adminPanelHandler.BanUser(Username, time);

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

        private void OnPromoteButtonClicked()
        {
            _popupOpener.Title = "Promote";
            _popupOpener.Subtitle = $"Do You want promote {Username}?";

            var promoteButtonInfo = new ButtonInfo
            {
                Label = "Promote",
                ClosePopupWhenClicked = true,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            promoteButtonInfo.OnClickedEvent.AddListener(Promote);
            
            _popupOpener.Buttons.Add(promoteButtonInfo);
            
            _popupOpener.OpenPopup();
        }
        
        private void Promote() => _adminPanelHandler.PromoteMember(Username);
    }
}