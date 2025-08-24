using System;
using EOSLobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;
using UnityEngine.Serialization;
using LobbyData = Code.Network.Lobby.Data.LobbyData;
    
namespace Code.Network.Lobby
{
    [DefaultExecutionOrder(-10)]
    public class LobbyVariables : MonoBehaviour
    {
        [SerializeField] private FishyEOS fishyEOS;
        
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
        public GameObject hostIndicator;

        public static LobbyVariables Instance;
        
        private ProductUserId _productUserId;

        private string _cashedHostId;
        
        public ProductUserId ProductUserId
        {
            get => _productUserId;
            set { _productUserId = value; productUserId = value.ToString(); }
        }
        
        public AuthData AuthData => authData;

        private void OnValidate()
        {
            fishyEOS ??= GetComponent<FishyEOS>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if(_cashedHostId == fishyEOS.RemoteProductUserId)
                return;
            _cashedHostId = fishyEOS.RemoteProductUserId;
            hostIndicator.SetActive(_cashedHostId == productUserId);
        }
    }
}