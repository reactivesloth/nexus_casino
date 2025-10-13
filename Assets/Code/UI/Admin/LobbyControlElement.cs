using System;
using System.Linq;
using Code.Chat;
using Code.Network.Lobby;
using Code.UI.Popup;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class LobbyControlElement : ControlElement
    {
        [SerializeField] private TMP_Text idText;
        [SerializeField] private TMP_Text adminsStatusText;
        [SerializeField] private TMP_Text playersCountText;
        [SerializeField] private TMP_Text privateText;

        [SerializeField] private Button moveToButton;

        private string _lobbyId;
        private NexusModularPopupOpener _popupOpener;
        private AdminPanelHandler _adminPanelHandler;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            moveToButton.onClick.AddListener(OnMoveToButtonClick);
        }

        private void OnDisable()
        {
            moveToButton.onClick.RemoveListener(OnMoveToButtonClick);
        }

        public void Init(LobbyDetails lobby)
        {
            Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(lobby, out var info);
            if (!info.HasValue)
            {
                Destroy(gameObject);
                return;
            }

            var lobbyId = info.Value.LobbyId;
            _lobbyId = lobbyId;

            var nameResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.Name, out var nameAttr);
            var lobbyName = nameResult == Result.Success
                ? nameAttr.Value.Data.Value.Value.AsUtf8.ToString()
                : string.Empty;

            var hostInResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.HostIn, out var hostInAttr);
            var hostIn = hostInResult == Result.Success
                ? hostInAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var moderInResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.ModerIn, out var moderInAttr);
            var moderIn = moderInResult == Result.Success
                ? moderInAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var adminInResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.AdminIn, out var adminInAttr);
            var adminIn = adminInResult == Result.Success
                ? adminInAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var privateResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.Private, out var privateAttr);
            var isPrivate = privateResult == Result.Success
                ? privateAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var maxPlayersCount = info.Value.MaxMembers;
            var currentPlayersCount = Network.Lobby.EOSCoroutines.Lobby.GetMembers(lobby).Count;

            titleDisplayText.text = lobbyName;
            idText.text = lobbyId;
            playersCountText.text = $"{currentPlayersCount}/{maxPlayersCount}";
            privateText.text = isPrivate ? "Private" : "Public";

            var adminText = adminIn ? "Admin\n" : string.Empty;
            var hostText = hostIn ? "Host\n" : string.Empty;
            var moderText = moderIn ? "Moderator\n" : string.Empty;
            adminsStatusText.text = $"{adminText}{hostText}{moderText}";

            SearchKey = lobbyName;
        }

        private void OnMoveToButtonClick()
        {
            _popupOpener.Title = "Move To Lobby";
            _popupOpener.Subtitle = $"Lobby {_lobbyId}";

            var addPlayerButton = new ButtonInfo
            {
                Label = "Add Player",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            addPlayerButton.OnClickedEvent.AddListener(AddPlayerToPopup);

            var removePlayerButton = new ButtonInfo
            {
                Label = "Remove Player",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            removePlayerButton.OnClickedEvent.AddListener(RemovePlayerFromPopup);

            var movePlayersButton = new ButtonInfo
            {
                Label = "Move Players",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            movePlayersButton.OnClickedEvent.AddListener(OnMoveClicked);

            _popupOpener.Buttons.Add(addPlayerButton);
            _popupOpener.Buttons.Add(removePlayerButton);
            _popupOpener.Buttons.Add(movePlayersButton);

            _popupOpener.OpenPopup();
        }

        private void AddPlayerToPopup()
        {
            _popupOpener.LastPopup.AddDropdown("Player",
                LobbyVariables.Instance.currentLobby.lobbyMembers.Select(m => m.displayName).ToArray());
        }

        private void RemovePlayerFromPopup()
        {
            if (_popupOpener.LastPopup.Inputs.Count > 0)
                _popupOpener.LastPopup.RemoveInputAt(_popupOpener.Inputs.Count - 1);
        }

        private void OnMoveClicked()
        {
            for (var i = 0; i < _popupOpener.Inputs.Count; i++)
            {
                var playerName = _popupOpener.LastPopup.GetInputValue(i);
                _adminPanelHandler.MoveUserToRoom(playerName, _lobbyId);
            }

            _popupOpener.ClosePopup();
        }
    }
}