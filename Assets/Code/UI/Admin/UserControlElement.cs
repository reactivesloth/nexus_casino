using Code.Network;
using Code.Network.Player;
using Code.UI.Popup;
using Code.Utility;
using PurrNet;
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
        
        private AdminPanelHandler _adminPanelHandler;
        private NexusModularPopupOpener _popupOpener;
        private PlayerUI _playerUI;

        private string _username;
        private MeSchema _playerData;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
            
            voiceImage.color = Color.clear;
            hostIndicator.SetActive(false);
        }

        private void OnEnable()
        {
            _adminPanelHandler.MutedDictionary.onChanged += OnMutedDictionaryChange;

            kickButton.onClick.AddListener(OnKickClicked);
            banButton.onClick.AddListener(OnBanClicked);
            muteChatButton.onClick.AddListener(OnMuteChatClicked);
            unmuteChatButton.onClick.AddListener(OnUnmuteChatClicked);
            muteVoiceButton.onClick.AddListener(OnMuteVoiceClicked);
            unmuteVoiceButton.onClick.AddListener(OnUnmuteVoiceClicked);
            toggleOffVoiceButton.onClick.AddListener(OnToggleOffVoiceClicked);
        }

        private void OnDisable()
        {
            _adminPanelHandler.MutedDictionary.onChanged -= OnMutedDictionaryChange;

            kickButton.onClick.RemoveListener(OnKickClicked);
            banButton.onClick.RemoveListener(OnBanClicked);
            muteChatButton.onClick.RemoveListener(OnMuteChatClicked);
            unmuteChatButton.onClick.RemoveListener(OnUnmuteChatClicked);
            muteVoiceButton.onClick.RemoveListener(OnMuteVoiceClicked);
            unmuteVoiceButton.onClick.RemoveListener(OnUnmuteVoiceClicked);
            toggleOffVoiceButton.onClick.RemoveListener(OnToggleOffVoiceClicked);
        }

        private void OnDestroy()
        {
            if(_playerUI != null)
            {
                _playerUI.IsVoiceHeld.onChanged -= IsVoiceHeldOnOnChange;
                _playerUI.IsVoiceMuted.onChanged -= IsVoiceMutedOnOnChange;
            }
        }

        private void OnMutedDictionaryChange(SyncDictionaryChange<string, AdminPanelHandler.MuteStateSync> change)
        {
            if (change.key == _username)
                SetMutedButtonsState(change.value.muteChat, change.value.muteVoice);
        }


        public void Init(MeSchema playerData)
        {
            _playerData = playerData;
            
            titleDisplayText.text = _username = playerData.username;
            roleText.text = playerData.role;

            if (_adminPanelHandler.MutedDictionary.TryGetValue(_username, out var muteState))
                SetMutedButtonsState(muteState.muteChat, muteState.muteVoice);
            else
                SetMutedButtonsState(false, false);

            SearchKey = _username;

            kickButton.interactable = banButton.interactable = muteChatButton.interactable =
                unmuteChatButton.interactable = muteVoiceButton.interactable =
                    unmuteVoiceButton.interactable = !playerData.IsAdminRole;

            _playerUI = PlayerUI.GetByPlayerName(_username);
            if(_playerUI != null)
            {
                _playerUI.IsVoiceHeld.onChanged += IsVoiceHeldOnOnChange;
                _playerUI.IsVoiceMuted.onChanged += IsVoiceMutedOnOnChange;
                hostIndicator.SetActive(_playerUI.IsHost);
                UpdateVoiceStatus();
            }
        }
        
        private void IsVoiceHeldOnOnChange(bool next) => UpdateVoiceStatus();
        
        private void IsVoiceMutedOnOnChange(bool next) => UpdateVoiceStatus();

        private void UpdateVoiceStatus()
        {
            var isVoiceMuted = _playerUI.IsVoiceMuted.value;
            var isVoiceHeld = _playerUI.IsVoiceHeld.value;
            voiceImage.color = isVoiceMuted ? Color.red : isVoiceHeld ? Color.green : Color.clear;
        }

        private void OnKickClicked()
        {
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.players.kick");//"Kick";
            _popupOpener.Subtitle = LocalizationHelper.GetLocalizedString("admin.players.kick.answer", "username", _username);//$"Do You want kick {Username}?";

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

        private void KickAction()
        {
            _adminPanelHandler.Kick(_username);
        }

        private void OnBanClicked()
        {
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.players.ban");//"Ban";
                _popupOpener.Subtitle = LocalizationHelper.GetLocalizedString("admin.players.ban.answer", "username", _username);//$"Do You want ban {Username}?";

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

        private void BanAction(int time)
        {
            _adminPanelHandler.BanUser(_username, time);
        }

        private void OnMuteChatClicked()
        {
            _adminPanelHandler.MuteChat(_username);
        }

        private void OnUnmuteChatClicked()
        {
            _adminPanelHandler.UnmuteChat(_username);
        }

        private void OnMuteVoiceClicked()
        {
            _adminPanelHandler.MuteVoice(_username);
        }

        private void OnUnmuteVoiceClicked()
        {
            _adminPanelHandler.UnmuteVoice(_username);
        }

        private void OnToggleOffVoiceClicked()
        {
            _adminPanelHandler.ToggleOffVoice(_username);
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
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.players.promote");
            _popupOpener.Subtitle = LocalizationHelper.GetLocalizedString("admin.players.promote.answer", "username", _username);

            var promoteButtonInfo = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("admin.players.promote"),
                ClosePopupWhenClicked = true,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            
            _popupOpener.Buttons.Add(promoteButtonInfo);

            _popupOpener.OpenPopup();
        }
    }
}