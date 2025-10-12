using System;
using Code.Network.Lobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class LobbyControlElement: ControlElement
    {
        [SerializeField] private TMP_Text idText;
        [SerializeField] private TMP_Text adminsStatusText;
        [SerializeField] private TMP_Text playersCountText;
        [SerializeField] private TMP_Text privateText;
        
        [SerializeField] private Button moveToButton;

        private string _lobbyId;

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
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
            //TODO: Open popup with ove control 
        }
    }
}