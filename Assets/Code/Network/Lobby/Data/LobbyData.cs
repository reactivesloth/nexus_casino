using System;
using System.Collections.Generic;
using System.Linq;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;

namespace Code.Network.Lobby.Data
{
    [Serializable]
    public class LobbyData
    {
        public string lobbyId;
        public string lobbyName;
        public uint maxPlayers;
        public List<LobbyMember> lobbyMembers = new();
        public string[] attributeKeys;
        public string[] attributeValues;

        public Dictionary<string, string> Attributes => 
            attributeKeys
                .Select((key, index) => new { key, value = attributeValues.ElementAtOrDefault(index) })
                .Where(pair => pair.key != null && pair.value != null)
                .ToDictionary(pair => pair.key, pair => pair.value);

        [Serializable]
        public class LobbyMember
        {
            public string productUserId;
            public string displayName;
            public LobbyMemberStatus status;
            public string[] attributeKeys;
            public string[] attributeValues;
            
            public Dictionary<string, string> Attributes => 
                attributeKeys
                    .Select((key, index) => new { key, value = attributeValues.ElementAtOrDefault(index) })
                    .Where(pair => pair.key != null && pair.value != null)
                    .ToDictionary(pair => pair.key, pair => pair.value);

            private ProductUserId _productUserId;

            public ProductUserId ProductUserId
            {
                get => _productUserId;
                set
                {
                    _productUserId = value;
                    productUserId = value.ToString();
                }
            }
        }
    }
}