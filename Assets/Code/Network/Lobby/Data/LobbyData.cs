﻿using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;

namespace Code.Network.Lobby.Data
{
    [Serializable]
    public class LobbyData
    {
        public string lobbyId;
        public string lobbyName;
        public uint   maxPlayers;

        public List<LobbyMember> lobbyMembers = new();

        public string[] attributeKeys;
        public string[] attributeValues;

        /// <summary>
        /// Без LINQ: аккуратно собираем словарь только по валидным парам key/value.
        /// </summary>
        public Dictionary<string, string> Attributes
        {
            get
            {
                var dict = new Dictionary<string, string>(attributeKeys != null ? attributeKeys.Length : 0, StringComparer.Ordinal);
                if (attributeKeys == null || attributeValues == null) return dict;

                int count = Math.Min(attributeKeys.Length, attributeValues.Length);
                for (int i = 0; i < count; i++)
                {
                    var k = attributeKeys[i];
                    var v = attributeValues[i];
                    if (!string.IsNullOrEmpty(k) && v != null)
                    {
                        // последний дубликат перезапишет предыдущий — ожидаемое поведение
                        dict[k] = v;
                    }
                }

                return dict;
            }
        }

        [Serializable]
        public class LobbyMember
        {
            public string productUserId;
            public string displayName;
            public LobbyMemberStatus status;

            public string[] attributeKeys;
            public string[] attributeValues;

            public Dictionary<string, string> Attributes
            {
                get
                {
                    var dict = new Dictionary<string, string>(attributeKeys != null ? attributeKeys.Length : 0, StringComparer.Ordinal);
                    if (attributeKeys == null || attributeValues == null) return dict;

                    int count = Math.Min(attributeKeys.Length, attributeValues.Length);
                    for (int i = 0; i < count; i++)
                    {
                        var k = attributeKeys[i];
                        var v = attributeValues[i];
                        if (!string.IsNullOrEmpty(k) && v != null)
                            dict[k] = v;
                    }

                    return dict;
                }
            }

            private ProductUserId _productUserId;

            public ProductUserId ProductUserId
            {
                get => _productUserId;
                set
                {
                    _productUserId = value;
                    productUserId = value != null ? value.ToString() : string.Empty;
                }
            }
        }
    }
}
