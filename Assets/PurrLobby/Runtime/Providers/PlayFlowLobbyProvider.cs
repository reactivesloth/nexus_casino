using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using PlayFlow;
using System.Linq;
using System.Threading;
using PurrNet.Logging;

namespace PurrLobby.Providers
{
    public class PlayFlowLobbyProvider : MonoBehaviour, ILobbyProvider
    {
        [SerializeField] private bool initializeOnStart = false;
        [SerializeField] private string region = "eu-west";

        public event UnityAction<string> OnLobbyJoinFailed;
        public event UnityAction OnLobbyLeft;
        public event UnityAction<Lobby> OnLobbyUpdated;
        public event UnityAction<List<LobbyUser>> OnLobbyPlayerListUpdated;
        public event UnityAction<List<FriendUser>> OnFriendListPulled;
        public event UnityAction<string> OnError;

        private string _localUserId;

        private void Start()
        {
            if (initializeOnStart)
            {
                _ = InitializeAsync();
            }
        }

        public async Task InitializeAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            _localUserId = SystemInfo.deviceUniqueIdentifier;

            PlayFlowLobbyManagerV2.Instance.Initialize(_localUserId, () =>
            {
                SubscribeToEvents();
                tcs.SetResult(true);
            });

            await tcs.Task;
        }

        private void SubscribeToEvents()
        {
            var events = PlayFlowLobbyManagerV2.Instance.Events;
            
            events.OnPlayerJoined.AddListener(_ => RefreshLobbyState());
            events.OnPlayerLeft.AddListener(_ => RefreshLobbyState());
            events.OnLobbyUpdated.AddListener(_ => RefreshLobbyState());
            events.OnLobbyLeft.AddListener(() => OnLobbyLeft?.Invoke());
        }

        public void Shutdown()
        {
            var events = PlayFlowLobbyManagerV2.Instance.Events;

            events.OnPlayerJoined.RemoveAllListeners();
            events.OnPlayerLeft.RemoveAllListeners();
            events.OnLobbyUpdated.RemoveAllListeners();
            events.OnLobbyLeft.RemoveAllListeners();
        }

        public async Task<List<FriendUser>> GetFriendsAsync(LobbyManager.FriendFilter filter)
        {
            return await Task.FromResult(new List<FriendUser>());
        }

        public async Task InviteFriendAsync(FriendUser user)
        {
            Debug.LogWarning("Invite system relies on external platform integration.");
            await Task.CompletedTask;
        }

        public async Task<Lobby> CreateLobbyAsync(int maxPlayers, Dictionary<string, string> lobbyProperties = null)
        {
            var tcs = new TaskCompletionSource<Lobby>();

            var customSettings = lobbyProperties?.ToDictionary(k => k.Key, v => (object)v.Value) ??
                                 new Dictionary<string, object>();

            if (!customSettings.ContainsKey("Name")) customSettings["Name"] = $"{_localUserId}'s Lobby";
            customSettings.TryAdd("Started", "False");

            PlayFlowLobbyManagerV2.Instance.CreateLobby(
                name: customSettings.TryGetValue("Name", out var setting) ? setting.ToString() : "New Lobby",
                maxPlayers: maxPlayers,
                isPrivate: false,
                allowLateJoin: true,
                region: region,
                customSettings: customSettings,
                onSuccess: pfLobby =>
                {
                    var lobby = UpdateLobby(pfLobby);
                    OnLobbyUpdated?.Invoke(lobby);
                    tcs.SetResult(lobby);
                },
                onError: error =>
                {
                    OnError?.Invoke(error);
                    tcs.SetResult(default);
                }
            );

            return await tcs.Task;
        }

        public async Task LeaveLobbyAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            PlayFlowLobbyManagerV2.Instance.LeaveLobby(() =>
            {
                OnLobbyLeft?.Invoke();
                tcs.SetResult(true);
            });

            await tcs.Task;
        }

        public async Task LeaveLobbyAsync(string lobbyId)
        {
            if (PlayFlowLobbyManagerV2.Instance.IsInLobby && PlayFlowLobbyManagerV2.Instance.CurrentLobby.id == lobbyId)
            {
                await LeaveLobbyAsync();
            }

            await Task.CompletedTask;
        }

        public async Task<Lobby> JoinLobbyAsync(string lobbyId)
        {
            var tcs = new TaskCompletionSource<Lobby>();

            PlayFlowLobbyManagerV2.Instance.JoinLobby(
                lobbyId,
                onSuccess: pfLobby =>
                {
                    var lobby = UpdateLobby(pfLobby);
                    OnLobbyUpdated?.Invoke(lobby);
                    tcs.SetResult(lobby);
                },
                onError: error =>
                {
                    OnLobbyJoinFailed?.Invoke(error);
                    tcs.SetResult(new Lobby { IsValid = false });
                }
            );

            return await tcs.Task;
        }

        public async Task<List<Lobby>> SearchLobbiesAsync(int maxRoomsToFind = 10,
            Dictionary<string, string> filters = null)
        {
            var allLobbies = PlayFlowLobbyManagerV2.Instance.AvailableLobbies;
            var results = new List<Lobby>();

            foreach (var pfLobby in allLobbies)
            {
                if (results.Count >= maxRoomsToFind) break;

                var matches = true;
                if (filters != null)
                {
                    if (filters.Any(filter =>
                            pfLobby.settings == null || !pfLobby.settings.ContainsKey(filter.Key) ||
                            pfLobby.settings[filter.Key]?.ToString() != filter.Value))
                    {
                        matches = false;
                    }
                }

                if (matches)
                {
                    results.Add(UpdateLobby(pfLobby));
                }
            }

            return await Task.FromResult(results);
        }

        public async Task SetIsReadyAsync(string userId, bool isReady)
        {
            var testState = new Dictionary<string, object>
            {
                ["IsReady"] = isReady,
                ["UpdateTrigger"] = DateTime.UtcNow.Ticks.ToString()
            };

            PlayFlowLobbyManagerV2.Instance.UpdatePlayerState(testState,
                onSuccess: null,
                onError: error => { OnError?.Invoke(error); }
            );
            await Task.CompletedTask;
        }

        public async Task SetLobbyDataAsync(string key, string value)
        {
            if (PlayFlowLobbyManagerV2.Instance.IsHost)
            {
                var dict = new Dictionary<string, object> { { key, value } };
                PlayFlowLobbyManagerV2.Instance.UpdateLobby(customSettings: dict);
            }

            await Task.CompletedTask;
        }

        public async Task<string> GetLobbyDataAsync(string key)
        {
            if (PlayFlowLobbyManagerV2.Instance.CurrentLobby?.settings != null &&
                PlayFlowLobbyManagerV2.Instance.CurrentLobby.settings.TryGetValue(key, out var value))
            {
                return await Task.FromResult(value?.ToString());
            }

            return await Task.FromResult(string.Empty);
        }

        public async Task<List<LobbyUser>> GetLobbyMembersAsync()
        {
            if (!PlayFlowLobbyManagerV2.Instance.IsInLobby)
                return await Task.FromResult(new List<LobbyUser>());
            
            var members = new List<LobbyUser>();
            if (PlayFlowLobbyManagerV2.Instance.CurrentLobby != null)
            {
                foreach (var p in PlayFlowLobbyManagerV2.Instance.CurrentLobby.players)
                {
                    members.Add(new LobbyUser
                    {
                        Id = p,
                        IsReady = IsPlayerReady(PlayFlowLobbyManagerV2.Instance.CurrentLobby, p)
                    });
                }
            }

            return await Task.FromResult(members);
        }

        public async Task<string> GetLocalUserIdAsync()
        {
            return await Task.FromResult(PlayFlowLobbyManagerV2.Instance.PlayerId ?? _localUserId);
        }

        public async Task SetAllReadyAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            if(PlayFlowLobbyManagerV2.Instance.IsHost)
            {
                PlayFlowLobbyManagerV2.Instance.StartMatch(
                    onSuccess: _ =>
                    {
                        Debug.Log("Match starting! Waiting for server...");
                        tcs.SetResult(true);
                    },
                    onError: error =>
                    {
                        OnError?.Invoke(error);
                        tcs.SetResult(false);
                    });
            }
            
            await tcs.Task;
        }

        public async Task SetLobbyStartedAsync()
        {
            await Task.FromResult(Task.CompletedTask);
        }

        private void RefreshLobbyState()
        {
            if (PlayFlowLobbyManagerV2.Instance.IsInLobby)
            {
                var lobby = UpdateLobby(PlayFlowLobbyManagerV2.Instance.CurrentLobby);
                OnLobbyUpdated?.Invoke(lobby);
                OnLobbyPlayerListUpdated?.Invoke(lobby.Members);
            }
        }

        private static Lobby UpdateLobby(PlayFlow.Lobby pfLobby)
        {
            if (pfLobby == null) return new Lobby { IsValid = false };

            var properties = new Dictionary<string, string>();
            if (pfLobby.settings != null)
            {
                foreach (var kvp in pfLobby.settings)
                {
                    properties[kvp.Key] = kvp.Value?.ToString();
                }
            }

            var members = new List<LobbyUser>();
            if (pfLobby.players != null)
            {
                members.AddRange(pfLobby.players.Select(p => new LobbyUser
                {
                    Id = p,
                    DisplayName = GetDisplayName (pfLobby, p),
                    IsReady = IsPlayerReady(pfLobby, p)
                }));
            }
            
            return new Lobby
            {
                IsValid = true,
                LobbyId = pfLobby.id,
                Name = pfLobby.name,
                MaxPlayers = pfLobby.maxPlayers,
                Properties = properties,
                Members = members
            };

        }

        private static string GetDisplayName(PlayFlow.Lobby lobby, string player)
        {
            if (lobby.lobbyStateRealTime.TryGetValue(player, out var playerData) && playerData.TryGetValue("DisplayName", out var playerName))
                return playerName.ToString();
            
            return string.Empty;
        }
        
        private static bool IsPlayerReady(PlayFlow.Lobby lobby, string player)
        {
            if (lobby.lobbyStateRealTime.TryGetValue(player, out var playerData) && playerData.TryGetValue("IsReady", out var readyValue))
                return bool.Parse(readyValue.ToString());

            return false;
        }
    }
}