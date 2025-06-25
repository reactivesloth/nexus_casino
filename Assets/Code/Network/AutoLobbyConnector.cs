using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private void StartClientConnection()
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

        private void StartHostConnection()
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