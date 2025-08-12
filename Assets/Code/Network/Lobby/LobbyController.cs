using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Code.API;
using Code.Network.Lobby.EOSCoroutines;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet;
using UnityEngine;
using Random = UnityEngine.Random;
using Code.Network.Lobby.Data;
using FishNet.Plugins.FishyEOS.Util;
using PlayEveryWare.EpicOnlineServices;

namespace Code.Network.Lobby
{
    public sealed class LobbyController : MonoBehaviour
    {
        private Coroutine _pollCoroutine;

        // Сигналы наружу
        public event Action OnHostReady;
        public event Action OnClientReady;
        public event Action<string> OnHostChanged;
        public event Action<string> OnCurrentHostDisconnected;

        private void OnEnable()
        {
            if (LobbyEvents.Instance != null)
            {
                LobbyEvents.Instance.LobbyUpdateReceived.AddListener(OnLobbyAttributesUpdated);
                LobbyEvents.Instance.LobbyMemberUpdateReceived.AddListener(OnMembersUpdate);
                LobbyEvents.Instance.LobbyMemberStatusReceived.AddListener(OnLobbyMemberStatusReceived);
            }
        }

        private void OnDisable()
        {
            if (LobbyEvents.Instance != null)
            {
                LobbyEvents.Instance.LobbyUpdateReceived.RemoveListener(OnLobbyAttributesUpdated);
                LobbyEvents.Instance.LobbyMemberUpdateReceived.RemoveListener(OnMembersUpdate);
                LobbyEvents.Instance.LobbyMemberStatusReceived.RemoveListener(OnLobbyMemberStatusReceived);
            }
        }

        public void StartPollingLobbies()
        {
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
            _pollCoroutine = StartCoroutine(PollLobbiesRoutine());
        }

        public void StopPollingLobbies()
        {
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
        }

        private IEnumerator PollLobbiesRoutine()
        {
            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            while (enabled)
            {
                var vars = LobbyVariables.Instance;
                if (vars == null) yield break;

                vars.lobbyPopupUI.Show("Searching lobby...", "");
                yield return LobbySearchLobbies.Run(out var search, localUserId);

                vars.searchResults = search != null ? search.LobbyDetailsArray : Array.Empty<LobbyDetails>();

                // фильтрация по PRODUCT_VERSION == Application.version
                var filtered = new List<LobbyDetails>(vars.searchResults != null ? vars.searchResults.Length : 0);
                if (vars.searchResults != null)
                {
                    for (int i = 0; i < vars.searchResults.Length; i++)
                    {
                        var l = vars.searchResults[i];
                        if (l == null) continue;
                        var verRes = EOSCoroutines.Lobby.GetAttribute(l, "PRODUCT_VERSION", out var verAttr);
                        if (verRes == Result.Success && verAttr.HasValue && verAttr.Value.Data.Value.Value.AsUtf8 == Application.version)
                            filtered.Add(l);
                    }
                }

                if (filtered.Count == 0)
                {
                    StartCoroutine(OnHobbyLobbyClickedRoutine());
                }
                else
                {
                    bool joined = false;
                    // копия списка
                    var pool = new List<LobbyDetails>(filtered.Count);
                    for (int i = 0; i < filtered.Count; i++) pool.Add(filtered[i]);

                    while (pool.Count > 0)
                    {
                        int idx = Random.Range(0, pool.Count);
                        var candidate = pool[idx];

                        EOSCoroutines.Lobby.GetLobbyInfo(candidate, out var info);
                        uint maxMembers = info.HasValue ? info.Value.MaxMembers : 0;
                        int memberCount = EOSCoroutines.Lobby.GetMembers(candidate).Count;

                        if (memberCount >= maxMembers || memberCount < 1)
                        {
                            pool.RemoveAt(idx);
                            continue;
                        }

                        StartCoroutine(OnJoinLobbyClickedRoutine(candidate));
                        joined = true;
                        break;
                    }

                    if (!joined)
                        StartCoroutine(OnHobbyLobbyClickedRoutine());
                }

                StopPollingLobbies();
                yield return new WaitForSeconds(LobbyVariables.Instance.pollLobbiesInterval);
            }
        }

        private IEnumerator OnHobbyLobbyClickedRoutine()
        {
            StopPollingLobbies();

            var vars = LobbyVariables.Instance;
            if (vars == null) yield break;

            vars.displayName.Value   = GetPlayerName();
            vars.hostLobbyName.Value = GenerateRandomLobbyName();
            vars.AuthData.displayName = vars.displayName;

            var lobbyName = vars.hostLobbyName;
            uint maxLobbyUsers = vars.maxLobbyUsers;
            string bucketId = vars.bucketId;

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Logging in...");
            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Creating Lobby...");
            yield return LobbyCreateLobby.Run(out var create, localUserId, maxLobbyUsers, bucketId);
            if (create == null || create.CallbackInfo?.ResultCode != Result.Success)
            {
                vars.hostLobbyName.Value = string.Empty;
                yield return vars.lobbyPopupUI.PromptCoroutine(out _, "Error", create?.CallbackInfo?.ResultCode.ToString());
                StartPollingLobbies();
                yield break;
            }

            string lobbyId = create.CallbackInfo?.LobbyId;
            var currentLobby = new LobbyData { lobbyId = lobbyId, lobbyName = lobbyName, maxPlayers = maxLobbyUsers };
            vars.currentLobby = currentLobby;

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Setting Lobby Name...");
            yield return LobbyUpdateLobby.Run(out var updVer, lobbyId, "PRODUCT_VERSION", Application.version);
            yield return LobbyUpdateLobby.Run(out var updName, lobbyId, "NAME", lobbyName.Value);
            if (updName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to update lobby name: {updName.CallbackInfo?.ResultCode}");

            var infoRes = EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId, localUserId);
            if (infoRes != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to get lobby details: {infoRes}");

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Display Name...");
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME", vars.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to set member name: {setName.CallbackInfo?.ResultCode}");

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Id...");
            yield return LobbyUpdateLobby.Run(out var setHost, lobbyId, "HOST_ID", localUserId.ToString());
            if (setHost.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to set HOST_ID: {setHost.CallbackInfo?.ResultCode}");

            vars.lobbyPopupUI.Hide();

            SetLobbyAttributes(currentLobby, lobbyDetails);
            OnHostConnectionReady();

            lobbyDetails.Release();
        }

        private IEnumerator OnJoinLobbyClickedRoutine(LobbyDetails lobbyDetails)
        {
            StopPollingLobbies();
            var vars = LobbyVariables.Instance;
            if (vars == null) yield break;

            if (string.IsNullOrEmpty(vars.displayName))
                vars.displayName.Value = GetPlayerName();

            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            if (lobbyDetails == null)
            {
                yield return vars.lobbyPopupUI.PromptCoroutine(out _, "Error", "Lobby details is null");
                StartPollingLobbies();
                yield break;
            }

            vars.lobbyPopupUI.Show("Joining Lobby...", "Please wait...");
            yield return LobbyJoinLobby.Run(out var join, localUserId, lobbyDetails);
            if (join == null || join.CallbackInfo?.ResultCode != Result.Success)
            {
                yield return vars.lobbyPopupUI.PromptCoroutine(out _, "Error", join?.CallbackInfo?.ResultCode.ToString());
                StartPollingLobbies();
                yield break;
            }

            vars.lobbyPopupUI.Show("Joining Lobby...", "Getting Lobby Info...");
            var infoRes = EOSCoroutines.Lobby.GetLobbyInfo(lobbyDetails, out var lobbyInfo);
            if (infoRes != Result.Success)
            {
                yield return vars.lobbyPopupUI.PromptCoroutine(out _, "Error", infoRes.ToString());
                StartPollingLobbies();
                yield break;
            }

            string lobbyId = lobbyInfo?.LobbyId;

            vars.lobbyPopupUI.Show("Joining Lobby...", "Getting Lobby Name...");
            var nameRes = EOSCoroutines.Lobby.GetAttribute(lobbyDetails, "NAME", out var lobbyNameAttr);
            if (nameRes != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to get lobby NAME: {nameRes}");

            vars.hostLobbyName.Value = lobbyNameAttr?.Data?.Value.AsUtf8;

            var currentLobby = new LobbyData
            {
                lobbyId = lobbyId,
                lobbyName = lobbyNameAttr?.Data?.Value.AsUtf8,
            };
            vars.currentLobby = currentLobby;

            vars.lobbyPopupUI.Show("Joining Lobby...", "Setting Local User Display Name...");
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME", vars.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to set member NAME: {setName.CallbackInfo?.ResultCode}");

            vars.lobbyPopupUI.Show("Joining Lobby...", "Getting Attributes...");
            SetLobbyAttributes(currentLobby, lobbyDetails);

            vars.lobbyPopupUI.Hide();
            OnClientConnectionReady();
        }

        private void OnLobbyAttributesUpdated(LobbyUpdateReceivedCallbackInfo e)
        {
            var vars = LobbyVariables.Instance;
            if (vars == null || vars.currentLobby == null) return;

            var localUserId = vars.ProductUserId;
            var result = EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, e.LobbyId, localUserId);
            if (result != Result.Success || lobbyDetails == null)
            {
                Debug.LogWarning($"[LobbyController] Failed to get lobby details. {result}");
                return;
            }

            var currentLobby = vars.currentLobby;

            // старый HOST_ID
            string oldHostId = string.Empty;
            if (currentLobby.Attributes != null && currentLobby.Attributes.TryGetValue("HOST_ID", out var oldVal))
                oldHostId = oldVal;

            // переносим атрибуты в массивы
            var attributes = EOSCoroutines.Lobby.GetAttributes(lobbyDetails);
            lobbyDetails.Release();

            int count = attributes != null ? attributes.Count : 0;
            currentLobby.attributeKeys = new string[count];
            currentLobby.attributeValues = new string[count];

            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var a = attributes[i];
                string k = a?.Data?.Key;
                string v = a?.Data?.Value.AsUtf8;
                currentLobby.attributeKeys[i] = k;
                currentLobby.attributeValues[i] = v;
                sb.Append(k).Append(' ');
            }
            Debug.Log(sb.ToString());

            // новый HOST_ID
            string newHostId = string.Empty;
            if (currentLobby.Attributes != null && currentLobby.Attributes.TryGetValue("HOST_ID", out var newVal))
                newHostId = newVal;

            if (!string.IsNullOrEmpty(oldHostId) && newHostId != oldHostId)
            {
                Debug.LogWarning($"[LobbyController] Host updated to {newHostId}");
                OnHostChanged?.Invoke(newHostId);
            }

            UpdateMembers();
        }

        private void OnLobbyMemberStatusReceived(LobbyMemberStatusReceivedCallbackInfo e)
        {
            UpdateMembers();

            if (e.CurrentStatus == LobbyMemberStatus.Promoted)
            {
                var vars = LobbyVariables.Instance;
                if (vars != null && e.TargetUserId != null &&
                    e.TargetUserId.ToString() == vars.ProductUserId.ToString())
                {
                    OnCurrentHostDisconnected?.Invoke(vars.ProductUserId.ToString());
                }
            }
        }

        private void OnMembersUpdate(LobbyMemberUpdateReceivedCallbackInfo _) => UpdateMembers();

        private void UpdateMembers()
        {
            var vars = LobbyVariables.Instance;
            if (vars == null || vars.currentLobby == null) return;

            var lobby = vars.currentLobby;
            string lobbyId = lobby.lobbyId;
            if (string.IsNullOrEmpty(lobbyId)) return;

            var localUserId = vars.ProductUserId;
            EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId, localUserId);
            if (lobbyDetails == null) return;

            var members = EOSCoroutines.Lobby.GetMembers(lobbyDetails);
            lobby.lobbyMembers.Clear();

            for (int i = 0; i < members.Count; i++)
            {
                var user = members[i];
                var nameRes = EOSCoroutines.Lobby.GetMemberAttribute(lobbyDetails, user, "NAME", out var nameAttr);
                if (nameRes != Result.Success)
                {
                    Debug.LogWarning($"[LobbyController] Failed to get member NAME: {nameRes} - {user}");
                }

                var attrs = EOSCoroutines.Lobby.GetMemberAttributes(lobbyDetails, user);
                // перенос в массивы без LINQ
                int ac = attrs != null ? attrs.Count : 0;
                var keys = new string[ac];
                var vals = new string[ac];
                for (int j = 0; j < ac; j++)
                {
                    var a = attrs[j];
                    keys[j] = a?.Data?.Key;
                    vals[j] = a?.Data?.Value.AsUtf8;
                }

                var member = new LobbyData.LobbyMember
                {
                    displayName = nameAttr?.Data?.Value.AsUtf8,
                    ProductUserId = user,
                    attributeKeys = keys,
                    attributeValues = vals
                };
                lobby.lobbyMembers.Add(member);
            }
        }

        private void OnHostConnectionReady()  => OnHostReady?.Invoke();
        private void OnClientConnectionReady() => OnClientReady?.Invoke();

        private void SetLobbyAttributes(LobbyData currentLobby, LobbyDetails lobbyDetails)
        {
            var attrs = EOSCoroutines.Lobby.GetAttributes(lobbyDetails);
            int count = attrs != null ? attrs.Count : 0;

            currentLobby.attributeKeys = new string[count];
            currentLobby.attributeValues = new string[count];

            for (int i = 0; i < count; i++)
            {
                var a = attrs[i];
                currentLobby.attributeKeys[i] = a?.Data?.Key;
                currentLobby.attributeValues[i] = a?.Data?.Value.AsUtf8;
            }
        }

        private string GetPlayerName() => ClientDataStorage.UserData.username;
        private string GenerateRandomLobbyName() => $"Lobby{Random.Range(0, 1000):000}";

        // === Ручное создание лобби ===
        public void CreateLobbyManual(string lobbyName, uint maxPlayers, string bucketId = null)
        {
            StartCoroutine(OnManualLobbyCreateRoutine(lobbyName, maxPlayers, bucketId ?? LobbyVariables.Instance.bucketId));
        }

        private IEnumerator OnManualLobbyCreateRoutine(string lobbyName, uint maxPlayers, string bucketId)
        {
            StopPollingLobbies();

            var vars = LobbyVariables.Instance;
            if (vars == null) yield break;

            vars.displayName.Value = GetPlayerName();
            vars.hostLobbyName.Value = lobbyName;
            vars.AuthData.displayName = vars.displayName;

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Logging in...");
            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Creating Lobby...");
            yield return LobbyCreateLobby.Run(out var create, localUserId, maxPlayers, bucketId);
            if (create == null || create.CallbackInfo?.ResultCode != Result.Success)
            {
                vars.hostLobbyName.Value = string.Empty;
                yield return vars.lobbyPopupUI.PromptCoroutine(out _, "Error", create?.CallbackInfo?.ResultCode.ToString());
                yield break;
            }

            string lobbyId = create.CallbackInfo?.LobbyId;

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Setting Lobby Name...");
            yield return LobbyUpdateLobby.Run(out var updVer, lobbyId, "PRODUCT_VERSION", Application.version);
            yield return LobbyUpdateLobby.Run(out var updName, lobbyId, "NAME", lobbyName);
            if (updName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to update lobby name: {updName.CallbackInfo?.ResultCode}");

            var infoRes = EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId, localUserId);
            if (infoRes != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to get lobby details: {infoRes}");

            var currentLobby = new LobbyData { lobbyId = lobbyId, lobbyName = lobbyName, maxPlayers = maxPlayers };
            vars.currentLobby = currentLobby;

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Display Name...");
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME", vars.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to set member NAME: {setName.CallbackInfo?.ResultCode}");

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Ready...");
            yield return LobbySetMemberAttribute.Run(out var setReady, lobbyId, localUserId, "READY", "Ready");
            if (setReady.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to set READY: {setReady.CallbackInfo?.ResultCode}");

            vars.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Id...");
            yield return LobbyUpdateLobby.Run(out var setHost, lobbyId, "HOST_ID", localUserId.ToString());
            if (setHost.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyController] Failed to set HOST_ID: {setHost.CallbackInfo?.ResultCode}");

            vars.lobbyPopupUI.Hide();

            SetLobbyAttributes(currentLobby, lobbyDetails);
            lobbyDetails.Release();

            OnHostConnectionReady();
        }

        // === Подключение по HOST_ID ===
        public void JoinLobbyByHostId(string hostId)
        {
            StartCoroutine(OnJoinLobbyByHostIdRoutine(hostId));
        }

        private IEnumerator OnJoinLobbyByHostIdRoutine(string hostId)
        {
            yield return LocalUser.Get(out var localUser);
            yield return LobbySearchLobbies.Run(out var search, localUser.Id);

            if (search != null && search.LobbyDetailsArray != null)
            {
                LobbyDetails found = null;
                for (int i = 0; i < search.LobbyDetailsArray.Length; i++)
                {
                    var l = search.LobbyDetailsArray[i];
                    if (l == null) continue;

                    var res = EOSCoroutines.Lobby.GetAttribute(l, "HOST_ID", out var attr);
                    if (res == Result.Success && attr.HasValue && attr.Value.Data.Value.Value.AsUtf8 == hostId)
                    {
                        found = l;
                        break;
                    }
                }

                if (found != null)
                {
                    StartCoroutine(OnJoinLobbyClickedRoutine(found));
                    yield break;
                }
            }

            Debug.LogWarning($"[LobbyController] Lobby with HOST_ID {hostId} not found.");
        }

        public LobbyDetails[] GetAllLobbies()
        {
            var vars = LobbyVariables.Instance;
            return vars != null && vars.searchResults != null ? vars.searchResults : Array.Empty<LobbyDetails>();
        }

        public LobbyDetails[] GetLobbiesByFilter(Func<LobbyDetails, bool> filter)
        {
            var all = GetAllLobbies();
            if (filter == null || all.Length == 0) return all;

            // без LINQ
            var list = new List<LobbyDetails>(all.Length);
            for (int i = 0; i < all.Length; i++)
                if (filter(all[i]))
                    list.Add(all[i]);

            return list.ToArray();
        }

        public void UpdateHost(string newHostId)
        {
            var mgr = EOS.GetManager();
            if (mgr != null)
                mgr.StartCoroutine(UpdateHostCoroutine(newHostId));
        }

        private IEnumerator UpdateHostCoroutine(string newHostId)
        {
            var vars = LobbyVariables.Instance;
            if (vars == null || vars.currentLobby == null) yield break;

            yield return LobbyUpdateLobby.Run(out var upd, vars.currentLobby.lobbyId, "HOST_ID", newHostId);
            if (upd.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogError($"[HostMigrator] Failed to set HOST_ID: {upd.CallbackInfo?.ResultCode}");
        }

        public void LeaveLobby()
        {
            var mgr = EOS.GetManager();
            if (mgr != null)
                mgr.StartCoroutine(LeaveLobbyRoutine());
        }

        private IEnumerator LeaveLobbyRoutine()
        {
            var vars = LobbyVariables.Instance;
            if (vars == null || vars.currentLobby == null) yield break;

            string lobbyId = vars.currentLobby.lobbyId;
            if (string.IsNullOrEmpty(lobbyId)) yield break;

            var userId = vars.ProductUserId;
            yield return LobbyLeaveLobby.Run(out var leave, lobbyId, userId);

            if (leave.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogError($"[LobbyController] Failed to leave lobby: {leave.CallbackInfo?.ResultCode}");
            else
                Debug.Log($"[LobbyController] Successfully left lobby {lobbyId}");
        }

        #region Internal

        private sealed class LocalUser
        {
            public ProductUserId Id { get; private set; }

            public static Coroutine Get(out LocalUser lu)
            {
                lu = new LocalUser();
                var vars = LobbyVariables.Instance;
                return vars != null ? vars.StartCoroutine(lu.GetCoroutine()) : null;
            }

            private IEnumerator GetCoroutine()
            {
                var vars = LobbyVariables.Instance;
                if (vars == null) yield break;

                if (vars.ProductUserId != null)
                {
                    Id = vars.ProductUserId;
                    yield break;
                }

                yield return Authenticate.Run(out var auth);
                Id = vars.ProductUserId = auth.LocalUserId;
            }
        }

        private sealed class Authenticate
        {
            public ProductUserId LocalUserId { get; private set; }

            public static Coroutine Run(out Authenticate a)
            {
                a = new Authenticate();
                var vars = LobbyVariables.Instance;
                return vars != null ? vars.StartCoroutine(a.AuthenticateCoroutine()) : null;
            }

            private IEnumerator AuthenticateCoroutine()
            {
                var vars = LobbyVariables.Instance;
                if (vars == null) yield break;

                var ad = vars.AuthData;
                yield return ConnectLogin.Run(
                    ad.loginCredentialType,
                    ad.externalCredentialType,
                    ad.id,
                    ad.token,
                    ad.displayName,
                    ad.automaticallyCreateDeviceId,
                    ad.automaticallyCreateConnectAccount,
                    (int)ad.timeout,
                    ad.authScopeFlags,
                    out var login
                );

                LocalUserId = login.CallbackInfo?.LocalUserId;
            }
        }

        #endregion
    }
}
