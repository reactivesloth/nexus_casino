using Code.API;
using Code.Chat;
using Code.Network.Lobby;
using Code.Network.Lobby.Data;
using Code.UI.Popup;
using Code.Utility;
using FishNet.Object.Synchronizing;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class UserControlElement : ControlElement
    {
        [SerializeField] private Image voiceImage;
        [SerializeField] private GameObject hostIndicator;
        
        [SerializeField] private TMP_Text roleText;

        [SerializeField] private Button kickButton;

        [SerializeField] private Button banButton;

        [Space, SerializeField] private Button muteChatButton;
        [SerializeField] private Button unmuteChatButton;

        [Space, SerializeField] private Button muteVoiceButton;
        [SerializeField] private Button unmuteVoiceButton;
        [SerializeField] private Button toggleOffVoiceButton;

        [SerializeField] private Button promoteButton;

        private AdminPanelHandler _adminPanelHandler;
        private LobbyData.LobbyMember _lobbyMemberData;
        private NexusModularPopupOpener _popupOpener;
        private PlayerUI _playerUI;

        private string Username => _lobbyMemberData.displayName;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
            
            voiceImage.color = Color.clear;
            hostIndicator.SetActive(false);
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
            toggleOffVoiceButton.onClick.AddListener(OnToggleOffVoiceClicked);

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
            toggleOffVoiceButton.onClick.RemoveListener(OnToggleOffVoiceClicked);

            promoteButton.onClick.RemoveListener(OnPromoteButtonClicked);
        }

        private void OnDestroy()
        {
            if(_playerUI != null)
            {
                _playerUI.IsVoiceHeld.OnChange -= IsVoiceHeldOnOnChange;
                _playerUI.IsVoiceMuted.OnChange -= IsVoiceMutedOnOnChange;
            }
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

            kickButton.interactable = banButton.interactable = muteChatButton.interactable =
                unmuteChatButton.interactable = muteVoiceButton.interactable =
                    unmuteVoiceButton.interactable = Username != ClientDataStorage.UserData.username;

            _playerUI = PlayerUI.GetByPlayerName(_lobbyMemberData.displayName);
            if(_playerUI != null)
            {
                _playerUI.IsVoiceHeld.OnChange += IsVoiceHeldOnOnChange;
                _playerUI.IsVoiceMuted.OnChange += IsVoiceMutedOnOnChange;
                hostIndicator.SetActive(_playerUI.IsHost);
                UpdateVoiceStatus();
            }
        }
        
        private void IsVoiceHeldOnOnChange(bool prev, bool next, bool asServer) => UpdateVoiceStatus();
        
        private void IsVoiceMutedOnOnChange(bool prev, bool next, bool asServer) => UpdateVoiceStatus();

        private void UpdateVoiceStatus()
        {
            var isVoiceMuted = _playerUI.IsVoiceMuted.Value;
            var isVoiceHeld = _playerUI.IsVoiceHeld.Value;
            voiceImage.color = isVoiceMuted ? Color.red : isVoiceHeld ? Color.green : Color.clear;
        }

        private void OnKickClicked()
        {
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.players.kick");//"Kick";
            _popupOpener.Subtitle = LocalizationHelper.GetLocalizedString("admin.players.kick.answer", "username", Username);//$"Do You want kick {Username}?";

            var yesButtonInfo = new ButtonInfo
            {
                ClosePopupWhenClicked = true,
                Label = LocalizationHelper.GetLocalizedString("admin.players.kick"),//"Kick",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            yesButtonInfo.OnClickedEvent.AddListener(KickAction);
            _popupOpener.Buttons.Add(yesButtonInfo);

            _popupOpener.OpenPopup();
        }

        private void KickAction() => _adminPanelHandler.Kick(Username);

        private void OnBanClicked()
        {
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.players.ban");//"Ban";
                _popupOpener.Subtitle = LocalizationHelper.GetLocalizedString("admin.players.ban.answer", "username", Username);//$"Do You want ban {Username}?";

            _popupOpener.Inputs.Add(new InputInfo
            {
                type = InputInfoType.InputField,
                labelName = LocalizationHelper.GetLocalizedString("admin.players.ban.time"), //"Time in minutes",
                contentType = TMP_InputField.ContentType.IntegerNumber
            });

            var yesButtonInfo = new ButtonInfo
            {
                ClosePopupWhenClicked = true,
                Label = LocalizationHelper.GetLocalizedString("admin.players.ban"), //"Ban",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            _popupOpener.Buttons.Add(yesButtonInfo);

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

        private void OnToggleOffVoiceClicked()
        {
            _adminPanelHandler.ToggleOffVoice(Username);
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
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.players.promote");//"Set as host";
            _popupOpener.Subtitle = LocalizationHelper.GetLocalizedString("admin.players.promote.answer", "username", Username);//$"Do You want promote {Username}?";

            var promoteButtonInfo = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("admin.players.promote"),//  "Promote",
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