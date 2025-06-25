using System.Collections;
using EOSLobby;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Lobby;
using FishNet;
using FishNet.Transporting.FishyEOSPlugin;
using UnityEngine;

public class AutoLobbyConnector : MonoBehaviour
{
    [SerializeField] private float searchDuration = 10f; // N секунд
    [SerializeField] private uint maxLobbyUsers = 5;
    [SerializeField] private string bucketId = "MyBucket";

    private void Start()
    {
        StartCoroutine(FindOrCreateLobbyRoutine());
    }

    private IEnumerator FindOrCreateLobbyRoutine()
    {
        // Получаем локального пользователя через внутренний класс LocalUser из LobbyCode.cs
        yield return LocalUser.Get(out var localUser);
        var localUserId = localUser.Id;

        float timer = 0f;
        LobbyDetails foundLobby = null;
        LobbyDetailsInfo? foundLobbyInfo = null;
        uint foundLobbyMemberCount = 0;

        // Ищем лобби N секунд
        while (timer < searchDuration)
        {
            yield return LobbySearchLobbies.Run(out var searchLobbies, localUserId);
            if (searchLobbies.LobbyDetailsArray != null)
            {
                foreach (var lobby in searchLobbies.LobbyDetailsArray)
                {
                    // Получаем инфо о лобби
                    Lobby.GetLobbyInfo(lobby, out var lobbyInfo);
                    // Получаем текущее количество игроков
                    var memberCountOptions = new LobbyDetailsGetMemberCountOptions();
                    var memberCount = lobby.GetMemberCount(ref memberCountOptions);
                    if (lobbyInfo.HasValue && memberCount < lobbyInfo.Value.MaxMembers)
                    {
                        foundLobby = lobby;
                        foundLobbyInfo = lobbyInfo;
                        foundLobbyMemberCount = memberCount;
                        break;
                    }
                }
            }
            if (foundLobby != null)
                break;

            yield return new WaitForSeconds(1f);
            timer += 1f;
        }

        if (foundLobby != null)
        {
            // Подключаемся к найденному лобби
            yield return LobbyJoinLobby.Run(out var joinLobby, localUserId, foundLobby);
            if (joinLobby.CallbackInfo?.ResultCode == Result.Success)
            {
                StartFishNetAsClient(foundLobby, localUserId);
            }
            else
            {
                Debug.LogWarning("Не удалось подключиться к лобби, создаём своё...");
                yield return CreateLobbyAndStart(localUserId);
            }
        }
        else
        {
            // Лобби не найдено — создаём своё
            yield return CreateLobbyAndStart(localUserId);
        }
    }

    private IEnumerator CreateLobbyAndStart(ProductUserId localUserId)
    {
        yield return LobbyCreateLobby.Run(out var createLobby, localUserId, maxLobbyUsers, bucketId);
        if (createLobby.CallbackInfo?.ResultCode == Result.Success)
        {
            // После создания лобби обязательно выставляем HOST_ID
            var lobbyId = createLobby.CallbackInfo?.LobbyId;
            if (!string.IsNullOrEmpty(lobbyId))
            {
                yield return LobbyUpdateLobby.Run(out var setId, lobbyId, "HOST_ID", localUserId.ToString());
            }
            StartFishNetAsHost(localUserId);
        }
        else
        {
            Debug.LogError("Не удалось создать лобби!");
        }
    }

    private void StartFishNetAsHost(ProductUserId localUserId)
    {
        var networkManager = InstanceFinder.NetworkManager;
        var fishyEOS = networkManager.GetComponent<FishyEOS>();
        fishyEOS.RemoteProductUserId = localUserId.ToString();
        // Заполняем AuthConnectData из LobbyVariables.Instance
        var auth = LobbyVariables.Instance.AuthData;
        fishyEOS.AuthConnectData.loginCredentialType = auth.loginCredentialType;
        fishyEOS.AuthConnectData.externalCredentialType = auth.externalCredentialType;
        fishyEOS.AuthConnectData.id = auth.id;
        fishyEOS.AuthConnectData.token = auth.token;
        fishyEOS.AuthConnectData.displayName = auth.loginCredentialType == LoginCredentialType.Developer ? "" : auth.displayName;
        fishyEOS.gameObject.SetActive(true);
        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();
    }

    private void StartFishNetAsClient(LobbyDetails lobbyDetails, ProductUserId localUserId)
    {
        var networkManager = InstanceFinder.NetworkManager;
        var fishyEOS = networkManager.GetComponent<FishyEOS>();
        // Получаем hostId из атрибута HOST_ID
        Lobby.GetAttribute(lobbyDetails, "HOST_ID", out var hostIdAttr);
        var hostId = hostIdAttr?.Data?.Value.AsUtf8;
        fishyEOS.RemoteProductUserId = hostId;
        // Заполняем AuthConnectData из LobbyVariables.Instance
        var auth = LobbyVariables.Instance.AuthData;
        fishyEOS.AuthConnectData.loginCredentialType = auth.loginCredentialType;
        fishyEOS.AuthConnectData.externalCredentialType = auth.externalCredentialType;
        fishyEOS.AuthConnectData.id = auth.id;
        fishyEOS.AuthConnectData.token = auth.token;
        fishyEOS.AuthConnectData.displayName = auth.loginCredentialType == LoginCredentialType.Developer ? "" : auth.displayName;
        fishyEOS.gameObject.SetActive(true);
        networkManager.ClientManager.StartConnection();
    }

    // Внутренний класс для получения локального пользователя (скопировано из LobbyCode.cs)
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
                var auth = LobbyVariables.Instance.AuthData;
                yield return ConnectLogin.Run(auth.loginCredentialType, auth.externalCredentialType, auth.id, auth.token, auth.displayName, auth.automaticallyCreateDeviceId, auth.automaticallyCreateConnectAccount, (int)auth.timeout, auth.authScopeFlags, out var login);
                LocalUserId = login.CallbackInfo?.LocalUserId;
            }
        }
    }
}