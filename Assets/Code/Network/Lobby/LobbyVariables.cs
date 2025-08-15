using EOSLobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;
using LobbyData = Code.Network.Lobby.Data.LobbyData;
    
namespace Code.Network.Lobby
{
    [DefaultExecutionOrder(-10)]
    public class LobbyVariables : MonoBehaviour
    {
        [Header("Self Player Variables")]
        public Bindable<string> displayName;
        public string productUserId;

        [Header("Lobby Variables")]
        public Bindable<string> hostLobbyName;
        public uint maxLobbyUsers = 5;
        public string bucketId = "MyBucket";
        public AuthData authData;
        public LobbyData currentLobby;
        public float pollLobbiesInterval = 5f;
        public LobbyDetails[] searchResults;

        [Header("Lobby References")]
        public LobbyPopup lobbyPopupUI;

        public static LobbyVariables Instance;
        
        private ProductUserId _productUserId;

        public ProductUserId ProductUserId
        {
            get => _productUserId;
            set { _productUserId = value; productUserId = value.ToString(); }
        }
        
        public AuthData AuthData => authData;

        private void Awake()
        {
            Instance = this;
        }
    }
}