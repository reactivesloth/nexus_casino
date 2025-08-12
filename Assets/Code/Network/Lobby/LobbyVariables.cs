using System;
using EOSLobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;
using LobbyData = Code.Network.Lobby.Data.LobbyData;

namespace Code.Network.Lobby
{
    [DefaultExecutionOrder(-10)]
    public sealed class LobbyVariables : MonoBehaviour
    {
        [Header("Self Player Variables")]
        public Bindable<string> displayName;
        public string productUserId;

        [Header("Lobby Variables")]
        public Bindable<string> hostLobbyName;
        public uint   maxLobbyUsers = 5;
        public string bucketId = "MyBucket";
        public AuthData authData;
        public LobbyData currentLobby;
        public float pollLobbiesInterval = 5f;

        [Tooltip("Результаты последнего поиска. Освобождаются автоматически при замене или уничтожении объекта.")]
        public LobbyDetails[] searchResults = Array.Empty<LobbyDetails>();

        [Header("Lobby References")]
        public LobbyPopup lobbyPopupUI;

        public static LobbyVariables Instance { get; private set; }

        private ProductUserId _productUserId;
        public ProductUserId ProductUserId
        {
            get => _productUserId;
            set { _productUserId = value; productUserId = value != null ? value.ToString() : string.Empty; }
        }

        public AuthData AuthData => authData;

        private void Awake() => Instance = this;

        private void OnDestroy() => ReleaseSearchResults();

        public void ReplaceSearchResults(LobbyDetails[] newResults)
        {
            ReleaseSearchResults();
            searchResults = newResults ?? Array.Empty<LobbyDetails>();
        }

        public void ReleaseSearchResults()
        {
            if (searchResults == null) return;
            for (int i = 0; i < searchResults.Length; i++)
            {
                var d = searchResults[i];
                if (d != null) d.Release();
            }
            searchResults = Array.Empty<LobbyDetails>();
        }
    }
}