using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.API;
using Code.Network.HostMigration;
using Code.Network.Lobby.EOSCoroutines;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet;
using UnityEngine;
using Random = UnityEngine.Random;
using Code.Network.Lobby.Data;

namespace Code.Network.Lobby
{
    public class LobbyController : MonoBehaviour
    {
        private Coroutine _pollCoroutine;

        // События для внешнего запуска сетевого соединения
        public event Action OnHostReady;

        public event Action OnClientReady;

        // Событие смены хоста
        public event Action<string> OnHostChanged;
        public event Action<string> OnCurrentHostDisconnected;

        private void OnEnable()
        {
            LobbyEvents.Instance.LobbyUpdateReceived.AddListener(OnLobbyUpdateHost);
            LobbyEvents.Instance.LobbyMemberUpdateReceived.AddListener(OnMembersUpdate);
            LobbyEvents.Instance.LobbyMemberStatusReceived.AddListener(OnLobbyMemberStatusReceived);
        }

        private void OnDisable()
        {
            LobbyEvents.Instance.LobbyUpdateReceived.RemoveListener(OnLobbyUpdateHost);
            LobbyEvents.Instance.LobbyMemberUpdateReceived.RemoveListener(OnMembersUpdate);
            LobbyEvents.Instance.LobbyMemberStatusReceived.RemoveListener(OnLobbyMemberStatusReceived);
        }

        public void StartPollingLobbies()
        {
            _pollCoroutine = StartCoroutine(PollLobbiesRoutine());
        }

        public void StopPollingLobbies()
        {
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
        }

        private IEnumerator PollLobbiesRoutine()
        {
            yield return LocalUser.Get(out var localUser);
            while (enabled)
            {
                LobbyVariables.Instance.lobbyPopupUI.Show("Searching lobby...", "");
                yield return LobbySearchLobbies.Run(out var searchLobbies, localUser.Id);

                // Обновляем массив найденных лобби
                LobbyVariables.Instance.searchResults = searchLobbies.LobbyDetailsArray;

                var lobbyList = searchLobbies.LobbyDetailsArray.ToList();

                for (var i = 0; i < lobbyList.Count; i++)
                {
                    var lobby = lobbyList[i];
                    var lobbyVersionRequest =
                        global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, "PRODUCT_VERSION",
                            out var versionAttribute);
                    if (lobbyVersionRequest != Result.Success || !versionAttribute.HasValue ||
                        versionAttribute?.Data?.Value.AsUtf8 != Application.version)
                        lobbyList.Remove(lobby);
                }

                if (lobbyList == null || lobbyList.Count == 0)
                    StartCoroutine(OnHobbyLobbyClickedRoutine());
                else
                {
                    bool isConnected = false;
                    var lobies = new List<LobbyDetails>(lobbyList);
                    while (lobies.Count > 0)
                    {
                        var randomLobby = lobies[Random.Range(0, lobies.Count)];

                        global::Code.Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(randomLobby, out var info);
                        var maxMembers = info.Value.MaxMembers;
                        var memberCount = global::Code.Network.Lobby.EOSCoroutines.Lobby.GetMembers(randomLobby).Count;
                        if (memberCount >= maxMembers)
                        {
                            lobies.Remove(randomLobby);
                            continue;
                        }

                        StartCoroutine(OnJoinLobbyClickedRoutine(randomLobby));
                        isConnected = true;
                        break;
                    }

                    if (!isConnected)
                        StartCoroutine(OnHobbyLobbyClickedRoutine());
                }

                StopPollingLobbies();

                yield return new WaitForSeconds(LobbyVariables.Instance.pollLobbiesInterval);
            }
        }

        private IEnumerator OnHobbyLobbyClickedRoutine()
        {
            StopPollingLobbies();

            LobbyVariables.Instance.displayName.Value = GetPlayerName();
            LobbyVariables.Instance.hostLobbyName.Value = GenerateRandomLobbyName();

            LobbyVariables.Instance.AuthData.displayName = LobbyVariables.Instance.displayName;
            var lobbyName = LobbyVariables.Instance.hostLobbyName;
            var maxLobbyUsers = LobbyVariables.Instance.maxLobbyUsers;
            var bucketId = LobbyVariables.Instance.bucketId;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Logging in...");
            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Creating Lobby...");
            yield return LobbyCreateLobby.Run(out var createLobby, localUserId, maxLobbyUsers, bucketId);
            if (createLobby.CallbackInfo?.ResultCode != Result.Success)
            {
                LobbyVariables.Instance.hostLobbyName.Value = string.Empty;
                yield return LobbyVariables.Instance.lobbyPopupUI.PromptCoroutine(out _, "Error",
                    createLobby.CallbackInfo?.ResultCode.ToString());
                StartPollingLobbies();
                yield break;
            }

            var lobbyId = createLobby.CallbackInfo?.LobbyId;
            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Lobby Name...");
            yield return LobbyUpdateLobby.Run(out var updateLobbyVersion, lobbyId, "PRODUCT_VERSION",
                Application.version);
            yield return LobbyUpdateLobby.Run(out var updateLobby, lobbyId, "NAME", lobbyName.Value);
            if (updateLobby.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby name: {updateLobby.CallbackInfo?.ResultCode}");

            var result =
                global::Code.Network.Lobby.EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId,
                    localUserId);
            if (result != Result.Success)
            {
                Debug.LogWarning($"[LobbyCode] Failed to get lobby details: {result}");
            }

            var currentLobby = new LobbyData { lobbyId = lobbyId, lobbyName = lobbyName, maxPlayers = maxLobbyUsers };
            LobbyVariables.Instance.currentLobby = currentLobby;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Display Name...");
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME",
                LobbyVariables.Instance.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby member name: {setName.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Ready...");
            yield return LobbySetMemberAttribute.Run(out var setReady, lobbyId, localUserId, "READY",
                "Ready");
            if (setReady.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to set lobby member ready: {setReady.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Id...");
            yield return LobbyUpdateLobby.Run(out var setId, lobbyId, "HOST_ID",
                localUserId.ToString());
            if (setId.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to set lobby member host id: {setId.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Hide();

            SetLobbyAttributes(currentLobby, lobbyDetails);
            lobbyDetails.Release();

            OnHostConnectionReady();
        }

        private IEnumerator OnJoinLobbyClickedRoutine(LobbyDetails lobbyDetails)
        {
            StopPollingLobbies();
            if (string.IsNullOrEmpty(LobbyVariables.Instance.displayName))
                LobbyVariables.Instance.displayName.Value = GetPlayerName();

            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            if (lobbyDetails == null)
            {
                yield return LobbyVariables.Instance.lobbyPopupUI.PromptCoroutine(out _, "Error",
                    "Lobby details is null");
                StartPollingLobbies();
                yield break;
            }

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Please wait...");
            yield return LobbyJoinLobby.Run(out var joinLobby, localUserId, lobbyDetails);
            if (joinLobby.CallbackInfo?.ResultCode != Result.Success)
            {
                yield return LobbyVariables.Instance.lobbyPopupUI.PromptCoroutine(out _, "Error",
                    joinLobby.CallbackInfo?.ResultCode.ToString());
                StartPollingLobbies();
                yield break;
            }

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Getting Lobby Info...");
            var getLobbyInfoResult =
                global::Code.Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(lobbyDetails, out var lobbyInfo);
            if (getLobbyInfoResult != Result.Success)
            {
                yield return LobbyVariables.Instance.lobbyPopupUI.PromptCoroutine(out _, "Error",
                    getLobbyInfoResult.ToString());
                StartPollingLobbies();
                yield break;
            }

            var lobbyId = lobbyInfo?.LobbyId;

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Getting Lobby Name...");
            var getAttributeResult =
                global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobbyDetails, "NAME",
                    out var lobbyNameAttribute);
            if (getAttributeResult != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to get lobby name: {getAttributeResult}");
            LobbyVariables.Instance.hostLobbyName.Value = lobbyNameAttribute?.Data?.Value.AsUtf8;

            var currentLobby = new LobbyData { lobbyId = lobbyId, lobbyName = lobbyNameAttribute?.Data?.Value.AsUtf8, };
            LobbyVariables.Instance.currentLobby = currentLobby;

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Setting Local User Display Name...");
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME",
                LobbyVariables.Instance.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby member name: {setName.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Getting Attributes...");
            SetLobbyAttributes(currentLobby, lobbyDetails);

            LobbyVariables.Instance.lobbyPopupUI.Hide();

            OnClientConnectionReady();
        }

        private void OnLobbyUpdateHost(LobbyUpdateReceivedCallbackInfo e)
        {
            var localUserId = LobbyVariables.Instance.ProductUserId;
            var currentLobby = LobbyVariables.Instance.currentLobby;
            if (currentLobby == null)
                return;

            var result =
                global::Code.Network.Lobby.EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, e.LobbyId,
                    localUserId);

            if (result != Result.Success)
            {
                Debug.LogWarning($"[LobbyCode] Failed to get lobby details. {result}");
                return;
            }

            if (currentLobby.attributeKeys == null)
                return;

            var oldHostId = currentLobby.Attributes.TryGetValue("HOST_ID", out var oldHostIdValue)
                ? oldHostIdValue
                : string.Empty;

            var attributes = global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttributes(lobbyDetails);
            lobbyDetails.Release();
            currentLobby.attributeKeys = new string[attributes.Count];
            currentLobby.attributeValues = new string[attributes.Count];

            for (var i = 0; i < attributes.Count; i++)
            {
                currentLobby.attributeKeys[i] = attributes[i]?.Data?.Key;
                currentLobby.attributeValues[i] = attributes[i]?.Data?.Value.AsUtf8;
            }

            var newHostId = currentLobby.Attributes.TryGetValue("HOST_ID", out var newHostIdValue)
                ? newHostIdValue
                : string.Empty;

            if (!string.IsNullOrEmpty(oldHostId) && newHostId != oldHostId)
            {
                // Вызываем событие смены хоста
                OnHostChanged?.Invoke(newHostId);
                InstanceFinder.NetworkManager.GetComponent<HostMigrator>().MarkMigrating();
            }

            UpdateMembers();
        }

        private void OnLobbyMemberStatusReceived(LobbyMemberStatusReceivedCallbackInfo arg0)
        {
            UpdateMembers();
        }

        private void OnMembersUpdate(LobbyMemberUpdateReceivedCallbackInfo e)
        {
            UpdateMembers();
        }

        private void UpdateMembers()
        {
            var lobby = LobbyVariables.Instance.currentLobby;
            if (lobby == null) return;

            var lobbyId = lobby.lobbyId;
            var lobbyMembers = lobby.lobbyMembers;
            var localUserId = LobbyVariables.Instance.ProductUserId;
            global::Code.Network.Lobby.EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId, localUserId);
            lobbyMembers.Clear();
            foreach (var productUserId in global::Code.Network.Lobby.EOSCoroutines.Lobby.GetMembers(lobbyDetails))
            {
                var getMemberAttributeResult =
                    global::Code.Network.Lobby.EOSCoroutines.Lobby.GetMemberAttribute(lobbyDetails, productUserId,
                        "NAME", out var memberName);
                if (getMemberAttributeResult != Result.Success)
                    Debug.LogWarning(
                        $"[LobbyCode] Failed to get member name. {getMemberAttributeResult} - {productUserId}");
                var allAttributes =
                    global::Code.Network.Lobby.EOSCoroutines.Lobby.GetMemberAttributes(lobbyDetails, productUserId);
                var member = new LobbyData.LobbyMember
                {
                    displayName = memberName?.Data?.Value.AsUtf8,
                    ProductUserId = productUserId,
                    attributeKeys = allAttributes.Select(x => x?.Data?.Key).Select(x => (string)x).ToArray(),
                    attributeValues = allAttributes.Select(x => x?.Data?.Value.AsUtf8).Select(x => (string)x).ToArray()
                };
                lobbyMembers.Add(member);
            }


            if (lobby.attributeKeys == null || !lobby.Attributes.TryGetValue("NEXT_HOST_ID", out var nextHostId))
                nextHostId = string.Empty;

            if (lobby.attributeKeys == null || !lobby.Attributes.TryGetValue("HOST_ID", out var currentHostId))
                currentHostId = string.Empty;
            var currentHostMember = lobby.lobbyMembers.FirstOrDefault(m => m.productUserId == currentHostId);
            Debug.Log(currentHostMember?.productUserId);
            if (currentHostMember == null)
                OnCurrentHostDisconnected?.Invoke(nextHostId);


            if (!InstanceFinder.NetworkManager.IsServerStarted)
                return;

            Debug.Log("OnMembersUpdate");

            if (lobby.attributeKeys == null)
                return;

            var nextHostMember = lobby.lobbyMembers.FirstOrDefault(m =>
                m.Attributes.TryGetValue("productUserId", out var id) && id == nextHostId &&
                id != LobbyVariables.Instance.productUserId);
            if (nextHostMember == null || nextHostMember.productUserId == lobby.Attributes["HOST_ID"])
                OnNextHostDisconnected();
        }

        private void OnNextHostDisconnected()
        {
            var lobby = LobbyVariables.Instance.currentLobby;
            var members = lobby.lobbyMembers;

            Debug.Log("[LobbyCode] OnNextHostDisconnected");
            var lobbyId = lobby.lobbyId;

            var nextHostMember = members.FirstOrDefault(m => m.productUserId != LobbyVariables.Instance.productUserId);
            var nextHostId = nextHostMember == null ? string.Empty : nextHostMember.productUserId;
            LobbyUpdateLobby.Run(out var updateLobby, lobbyId, "NEXT_HOST_ID", nextHostId);
        }

        private void OnHostConnectionReady()
        {
            OnHostReady?.Invoke();
        }

        private void OnClientConnectionReady()
        {
            OnClientReady?.Invoke();
        }

        private void SetLobbyAttributes(LobbyData currentLobby, LobbyDetails lobbyDetails)
        {
            var attributes = global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttributes(lobbyDetails);
            currentLobby.attributeKeys = attributes.Select(x => x?.Data?.Key).Select(x => (string)x).ToArray();
            currentLobby.attributeValues =
                attributes.Select(x => x?.Data?.Value.AsUtf8).Select(x => (string)x).ToArray();
        }

        private string GetPlayerName() => ClientDataStorage.UserData.username;
        private string GenerateRandomLobbyName() => $"Lobby{Random.Range(0, 1000):000}";

        // === Ручное создание лобби ===
        public void CreateLobbyManual(string lobbyName, uint maxPlayers, string bucketId = null)
        {
            StartCoroutine(OnManualLobbyCreateRoutine(lobbyName, maxPlayers,
                bucketId ?? LobbyVariables.Instance.bucketId));
        }

        private IEnumerator OnManualLobbyCreateRoutine(string lobbyName, uint maxPlayers, string bucketId)
        {
            StopPollingLobbies();

            LobbyVariables.Instance.displayName.Value = GetPlayerName();
            LobbyVariables.Instance.hostLobbyName.Value = lobbyName;

            LobbyVariables.Instance.AuthData.displayName = LobbyVariables.Instance.displayName;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Logging in...");
            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Creating Lobby...");
            yield return LobbyCreateLobby.Run(out var createLobby, localUserId, maxPlayers, bucketId);
            if (createLobby.CallbackInfo?.ResultCode != Result.Success)
            {
                LobbyVariables.Instance.hostLobbyName.Value = string.Empty;
                yield return LobbyVariables.Instance.lobbyPopupUI.PromptCoroutine(out _, "Error",
                    createLobby.CallbackInfo?.ResultCode.ToString());
                yield break;
            }

            var lobbyId = createLobby.CallbackInfo?.LobbyId;
            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Lobby Name...");
            yield return LobbyUpdateLobby.Run(out var updateLobbyVersion, lobbyId, "PRODUCT_VERSION",
                Application.version);
            yield return LobbyUpdateLobby.Run(out var updateLobby, lobbyId, "NAME", lobbyName);
            if (updateLobby.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby name: {updateLobby.CallbackInfo?.ResultCode}");

            var result =
                Code.Network.Lobby.EOSCoroutines.Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId, localUserId);
            if (result != Result.Success)
            {
                Debug.LogWarning($"[LobbyCode] Failed to get lobby details: {result}");
            }

            var currentLobby = new LobbyData { lobbyId = lobbyId, lobbyName = lobbyName, maxPlayers = maxPlayers };
            LobbyVariables.Instance.currentLobby = currentLobby;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Display Name...");
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME",
                LobbyVariables.Instance.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby member name: {setName.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Ready...");
            yield return LobbySetMemberAttribute.Run(out var setReady, lobbyId, localUserId, "READY",
                "Ready");
            if (setReady.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to set lobby member ready: {setReady.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Id...");
            yield return LobbyUpdateLobby.Run(out var setId, lobbyId, "HOST_ID",
                localUserId.ToString());
            if (setId.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to set lobby member host id: {setId.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Hide();

            SetLobbyAttributes(currentLobby, lobbyDetails);
            lobbyDetails.Release();

            OnHostConnectionReady();
        }

        // === Ручное подключение к лобби по HOST_ID ===
        public void JoinLobbyByHostId(string hostId)
        {
            StartCoroutine(OnJoinLobbyByHostIdRoutine(hostId));
        }

        private IEnumerator OnJoinLobbyByHostIdRoutine(string hostId)
        {
            yield return LocalUser.Get(out var localUser);
            yield return LobbySearchLobbies.Run(out var searchLobbies, localUser.Id);

            var lobby = searchLobbies.LobbyDetailsArray
                .FirstOrDefault(l =>
                    Code.Network.Lobby.EOSCoroutines.Lobby.GetAttribute(l, "HOST_ID", out var attr) ==
                    Epic.OnlineServices.Result.Success &&
                    attr?.Data.Value.Value.AsUtf8 == hostId);

            if (lobby != null)
                StartCoroutine(OnJoinLobbyClickedRoutine(lobby));
            else
                Debug.LogWarning($"[LobbyController] Lobby with HOST_ID {hostId} not found.");
        }

        // === Возврат всех лобби ===
        public LobbyDetails[] GetAllLobbies()
        {
            return LobbyVariables.Instance.searchResults ?? Array.Empty<LobbyDetails>();
        }

        // === Возврат списка лобби по фильтру ===
        public LobbyDetails[] GetLobbiesByFilter(Func<LobbyDetails, bool> filter)
        {
            var all = GetAllLobbies();
            return all.Where(filter).ToArray();
        }

        #region InternalClasses

        private class LocalUser
        {
            public ProductUserId Id { get; private set; }

            public static Coroutine Get(out LocalUser localUser)
            {
                localUser = new LocalUser();
                return LobbyVariables.Instance.StartCoroutine(localUser.GetCoroutine());
            }

            private IEnumerator GetCoroutine()
            {
                if (LobbyVariables.Instance.ProductUserId != null)
                {
                    Id = LobbyVariables.Instance.ProductUserId;
                    yield break;
                }

                yield return Authenticate.Run(out var authenticate);
                Id = LobbyVariables.Instance.ProductUserId = authenticate.LocalUserId;
            }
        }

        private class Authenticate
        {
            public ProductUserId LocalUserId { get; set; }

            public static Coroutine Run(out Authenticate authenticate)
            {
                authenticate = new Authenticate();
                return LobbyVariables.Instance.StartCoroutine(authenticate.AuthenticateCoroutine());
            }

            private IEnumerator AuthenticateCoroutine()
            {
                var loginCredentialType = LobbyVariables.Instance.AuthData.loginCredentialType;
                var externalCredentialType = LobbyVariables.Instance.AuthData.externalCredentialType;
                var id = LobbyVariables.Instance.AuthData.id;
                var token = LobbyVariables.Instance.AuthData.token;
                var displayName = LobbyVariables.Instance.AuthData.displayName;
                var automaticallyCreateDeviceId = LobbyVariables.Instance.AuthData.automaticallyCreateDeviceId;
                var automaticallyCreateConnectAccount =
                    LobbyVariables.Instance.AuthData.automaticallyCreateConnectAccount;
                var timeout = (int)LobbyVariables.Instance.AuthData.timeout;
                var scopeFlags = LobbyVariables.Instance.AuthData.authScopeFlags;
                yield return ConnectLogin.Run(loginCredentialType, externalCredentialType, id, token, displayName,
                    automaticallyCreateDeviceId, automaticallyCreateConnectAccount, timeout, scopeFlags,
                    out var login);
                LocalUserId = login.CallbackInfo?.LocalUserId;
            }
        }

        #endregion
    }
}