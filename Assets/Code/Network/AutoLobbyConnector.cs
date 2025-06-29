using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Network.HostMigration;
using EOSLobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Lobby;
using FishNet;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Network
{
    public class AutoLobbyConnector : MonoBehaviour
    {
        private Coroutine _pollCoroutine;

        private void Start()
        {
            StartPollingLobbies();
        }

        private void OnEnable()
        {
            LobbyEvents.Instance.LobbyUpdateReceived.AddListener(OnLobbyUpdateHost);
            LobbyEvents.Instance.LobbyMemberUpdateReceived.AddListener(OnMembersUpdate);
        }

        private void OnDisable()
        {
            LobbyEvents.Instance.LobbyUpdateReceived.RemoveListener(OnLobbyUpdateHost);
            LobbyEvents.Instance.LobbyMemberUpdateReceived.RemoveListener(OnMembersUpdate);
        }

        private void StartPollingLobbies()
        {
            _pollCoroutine = StartCoroutine(PollLobbiesRoutine());
        }

        private void StopPollingLobbies()
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

                var lobbyList = searchLobbies.LobbyDetailsArray.ToList();

                for (var i = 0; i < lobbyList.Count; i++)
                {
                    var lobby = lobbyList[i];
                    var lobbyVersionRequest = Lobby.GetAttribute(lobby, "PRODUCT_VERSION", out var versionAttribute);
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

                        Lobby.GetLobbyInfo(randomLobby, out var info);
                        var maxMembers = info.Value.MaxMembers;
                        var memberCount = Lobby.GetMembers(randomLobby).Count;
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

            LobbyVariables.Instance.displayName.Value = $"Player{Random.Range(0, 1000):000}";
            LobbyVariables.Instance.hostLobbyName.Value = $"Lobby{Random.Range(0, 1000):000}";

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

            var result = Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId, localUserId);
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

            var attributes = Lobby.GetAttributes(lobbyDetails);
            currentLobby.attributeKeys = attributes.Select(x => x?.Data?.Key).Select(x => (string)x).ToArray();
            currentLobby.attributeValues =
                attributes.Select(x => x?.Data?.Value.AsUtf8).Select(x => (string)x).ToArray();

            lobbyDetails.Release();

            StartHostConnection();
        }

        private IEnumerator OnJoinLobbyClickedRoutine(LobbyDetails lobbyDetails)
        {
            StopPollingLobbies();
            if (string.IsNullOrEmpty(LobbyVariables.Instance.displayName))
                LobbyVariables.Instance.displayName.Value = $"Player{Random.Range(0, 1000):000}";

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
            var getLobbyInfoResult = Lobby.GetLobbyInfo(lobbyDetails, out var lobbyInfo);
            if (getLobbyInfoResult != Result.Success)
            {
                yield return LobbyVariables.Instance.lobbyPopupUI.PromptCoroutine(out _, "Error",
                    getLobbyInfoResult.ToString());
                StartPollingLobbies();
                yield break;
            }

            var lobbyId = lobbyInfo?.LobbyId;

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Getting Lobby Name...");
            var getAttributeResult = Lobby.GetAttribute(lobbyDetails, "NAME", out var lobbyNameAttribute);
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
            var attributes = Lobby.GetAttributes(lobbyDetails);
            currentLobby.attributeKeys = attributes.Select(x => x?.Data?.Key).Select(x => (string)x).ToArray();
            currentLobby.attributeValues =
                attributes.Select(x => x?.Data?.Value.AsUtf8).Select(x => (string)x).ToArray();

            LobbyVariables.Instance.lobbyPopupUI.Hide();

            StartClientConnection();
        }

        public static void StartClientConnection()
        {
            var currentLobby = LobbyVariables.Instance.currentLobby;

            var hostIdIndex = Array.IndexOf(currentLobby.attributeKeys, "HOST_ID");
            if (hostIdIndex == -1)
            {
                Debug.LogWarning("[LobbyCode] Failed to get host id.");
                return;
            }

            var hostId = currentLobby.attributeValues[hostIdIndex];

            var networkManager = InstanceFinder.NetworkManager;
            var fishyEOS = networkManager.GetComponent<FishyEOS>();
            fishyEOS.RemoteProductUserId = hostId;
            fishyEOS.AuthConnectData.loginCredentialType = LobbyVariables.Instance.AuthData.loginCredentialType;
            fishyEOS.AuthConnectData.externalCredentialType =
                LobbyVariables.Instance.AuthData.externalCredentialType;
            fishyEOS.AuthConnectData.id = LobbyVariables.Instance.AuthData.id;
            fishyEOS.AuthConnectData.token = LobbyVariables.Instance.AuthData.token;
            fishyEOS.AuthConnectData.displayName =
                LobbyVariables.Instance.AuthData.loginCredentialType == LoginCredentialType.Developer
                    ? ""
                    : LobbyVariables.Instance.AuthData.displayName;
            fishyEOS.gameObject.SetActive(true);
            networkManager.ClientManager.StartConnection();

            LobbyVariables.Instance.lobbyGameUI.SetActive(true);
            LobbyVariables.Instance.lobbyGame.SetActive(true);
        }

        public static void StartHostConnection()
        {
            var networkManager = InstanceFinder.NetworkManager;
            var localUserId = LobbyVariables.Instance.ProductUserId;
            var fishyEOS = networkManager.GetComponent<FishyEOS>();
            fishyEOS.RemoteProductUserId = localUserId.ToString();
            fishyEOS.AuthConnectData.loginCredentialType = LobbyVariables.Instance.AuthData.loginCredentialType;
            fishyEOS.AuthConnectData.externalCredentialType = LobbyVariables.Instance.AuthData.externalCredentialType;
            fishyEOS.AuthConnectData.id = LobbyVariables.Instance.AuthData.id;
            fishyEOS.AuthConnectData.token = LobbyVariables.Instance.AuthData.token;
            fishyEOS.AuthConnectData.displayName =
                LobbyVariables.Instance.AuthData.loginCredentialType == LoginCredentialType.Developer
                    ? ""
                    : LobbyVariables.Instance.AuthData.displayName;
            fishyEOS.gameObject.SetActive(true);
            networkManager.ServerManager.StartConnection();
            networkManager.ClientManager.StartConnection();

            LobbyVariables.Instance.lobbyGameUI.SetActive(true);
            LobbyVariables.Instance.lobbyGame.SetActive(true);
        }

        private void OnLobbyUpdateHost(LobbyUpdateReceivedCallbackInfo e)
        {
            var localUserId = LobbyVariables.Instance.ProductUserId;
            var currentLobby = LobbyVariables.Instance.currentLobby;
            if (currentLobby == null)
                return;

            var result = Lobby.GetLobbyDetails(out var lobbyDetails, e.LobbyId, localUserId);

            if (result != Result.Success)
            {
                Debug.LogWarning($"[LobbyCode] Failed to get lobby details. {result}");
                return;
            }

            if (currentLobby.attributeKeys == null)
                return;
            var isCanGetHostAttr = currentLobby.attributeKeys.Contains("HOST_ID");
            var oldHostId = string.Empty;
            if (isCanGetHostAttr)
                oldHostId = currentLobby.attributeValues[Array.IndexOf(currentLobby.attributeKeys, "HOST_ID")];

            var attributes = Lobby.GetAttributes(lobbyDetails);
            lobbyDetails.Release();
            currentLobby.attributeKeys = new string[attributes.Count];
            currentLobby.attributeValues = new string[attributes.Count];

            for (var i = 0; i < attributes.Count; i++)
            {
                currentLobby.attributeKeys[i] = attributes[i]?.Data?.Key;
                currentLobby.attributeValues[i] = attributes[i]?.Data?.Value.AsUtf8;
            }

            isCanGetHostAttr = currentLobby.attributeKeys.Contains("HOST_ID");
            var newHostId = string.Empty;
            if (isCanGetHostAttr)
                newHostId = currentLobby.attributeValues[Array.IndexOf(currentLobby.attributeKeys, "HOST_ID")];

            if (!string.IsNullOrEmpty(oldHostId) && newHostId != oldHostId)
            {
                InstanceFinder.NetworkManager.GetComponent<HostMigrator>().MarkMigrating();
                StartClientConnection();
            }
        }

        private void OnMembersUpdate(LobbyMemberUpdateReceivedCallbackInfo e)
        {
            var lobby = LobbyVariables.Instance.currentLobby;
            if (lobby == null) return;

            var lobbyId = lobby.lobbyId;
            var lobbyMembers = lobby.lobbyMembers;
            var localUserId = LobbyVariables.Instance.ProductUserId;
            Lobby.GetLobbyDetails(out var lobbyDetails, lobbyId, localUserId);
            lobbyMembers.Clear();
            foreach (var productUserId in Lobby.GetMembers(lobbyDetails))
            {
                var getMemberAttributeResult =
                    Lobby.GetMemberAttribute(lobbyDetails, productUserId, "NAME", out var memberName);
                if (getMemberAttributeResult != Result.Success)
                    Debug.LogWarning(
                        $"[LobbyCode] Failed to get member name. {getMemberAttributeResult} - {productUserId}");
                var allAttributes = Lobby.GetMemberAttributes(lobbyDetails, productUserId);
                lobbyMembers.Add(new LobbyData.LobbyMember
                {
                    displayName = memberName?.Data?.Value.AsUtf8,
                    ProductUserId = productUserId,
                    attributeKeys = allAttributes.Select(x => x?.Data?.Key).Select(x => (string)x).ToArray(),
                    attributeValues = allAttributes.Select(x => x?.Data?.Value.AsUtf8).Select(x => (string)x).ToArray()
                });
            }

            if (!InstanceFinder.NetworkManager.IsServerStarted)
                return;

            Debug.Log("OnMembersUpdate");

            if (lobby.attributeKeys == null)
                return;

            var isCanGetNextHostAttr = lobby.attributeKeys.Contains("NEXT_HOST_ID");
            var nextHostId = string.Empty;

            if (isCanGetNextHostAttr)
                nextHostId = lobby.attributeValues[Array.IndexOf(lobby.attributeKeys, "NEXT_HOST_ID")];

            var nextHostMember = lobby.lobbyMembers.FirstOrDefault(m =>
                m.productUserId == nextHostId && m.productUserId != LobbyVariables.Instance.productUserId);
            if (nextHostMember == null)
                OnNextHostDisconnected();
        }

        private void OnNextHostDisconnected()
        {
            var members = LobbyVariables.Instance.currentLobby.lobbyMembers;
            var currentHostMember =
                members.FirstOrDefault(m => m.productUserId == LobbyVariables.Instance.productUserId);
            if (currentHostMember != null)
                members.Remove(currentHostMember);

            Debug.Log("[LobbyCode] OnNextHostDisconnected");
            var lobby = LobbyVariables.Instance.currentLobby;
            var lobbyId = lobby.lobbyId;

            var nextHostMember = lobby.lobbyMembers.FirstOrDefault();
            if (nextHostMember == null)
                return;
            LobbyUpdateLobby.Run(out var updateLobby, lobbyId, "NEXT_HOST_ID", nextHostMember.productUserId);
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