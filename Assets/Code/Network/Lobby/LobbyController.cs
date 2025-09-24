using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby.EOSCoroutines;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet;
using UnityEngine;
using Random = UnityEngine.Random;
using Code.Network.Lobby.Data;
using Code.Utility;
using FishNet.Plugins.FishyEOS.Util;
using PlayEveryWare.EpicOnlineServices;

namespace Code.Network.Lobby
{
    public class LobbyController : MonoBehaviour
    {
        private Coroutine _pollCoroutine;
        private Coroutine _pingCoroutine; // Корутина для пинга

        // События для внешнего запуска сетевого соединения
        public event Action OnHostReady;

        public event Action OnClientReady;

        // Событие смены хоста
        public event Action<string> OnHostChanged;
        public event Action<string> OnCurrentHostDisconnected;

        private void OnEnable()
        {
            LobbyEvents.Instance.LobbyUpdateReceived.AddListener(OnLobbyAttributesUpdated);
            LobbyEvents.Instance.LobbyMemberUpdateReceived.AddListener(OnMembersUpdate);
            LobbyEvents.Instance.LobbyMemberStatusReceived.AddListener(OnLobbyMemberStatusReceived);
        }

        private void OnDisable()
        {
            LobbyEvents.Instance.LobbyUpdateReceived.RemoveListener(OnLobbyAttributesUpdated);
            LobbyEvents.Instance.LobbyMemberUpdateReceived.RemoveListener(OnMembersUpdate);
            LobbyEvents.Instance.LobbyMemberStatusReceived.RemoveListener(OnLobbyMemberStatusReceived);
        }

        #region Ping Update

        // 🔹 Запуск обновления пинга каждые N секунд
        public void StartUpdatingPing(float intervalSeconds)
        {
            if (_pingCoroutine != null)
                StopCoroutine(_pingCoroutine);
            _pingCoroutine = StartCoroutine(UpdatePingRoutine(intervalSeconds));
        }


        public void StopUpdatingPing()
        {
            if (_pingCoroutine != null)
                StopCoroutine(_pingCoroutine);
        }

        private IEnumerator UpdatePingRoutine(float interval)
        {
            while (true)
            {
                yield return new WaitForSeconds(interval);


                var lobby = LobbyVariables.Instance.currentLobby;
                if (lobby == null || string.IsNullOrEmpty(lobby.lobbyId))
                    continue;


                var localUserId = LobbyVariables.Instance.ProductUserId;
                if (localUserId == null)
                    continue;

                var ping = GetCurrentPing();

                yield return LobbySetMemberAttribute.Run(out var setPing, lobby.lobbyId, localUserId, "PING",
                    ping.ToString());
                if (setPing.CallbackInfo?.ResultCode != Result.Success)
                    Debug.LogWarning($"[LobbyController] Failed to update ping: {setPing.CallbackInfo?.ResultCode}");
            }
        }

        private long GetCurrentPing()
        {
            var ping = InstanceFinder.TimeManager.RoundTripTime;
            var deduction = (long)(InstanceFinder.TimeManager.TickDelta * 2000d);

            return (long)Mathf.Max(1, ping - deduction);
        }

        #endregion

        public void StartPollingLobbies()
        {
            _pollCoroutine = StartCoroutine(PollLobbiesRoutineWithConnection());
        }

        public void StopPollingLobbies()
        {
            if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
        }

        public IEnumerator PollLobbiesRoutine()
        {
            yield return LocalUser.Get(out var localUser);
            yield return LobbySearchLobbies.Run(out var searchLobbies, localUser.Id);
            // 🔹 Сохраняем результаты поиска (для UI/отладки)
            LobbyVariables.Instance.searchResults = searchLobbies.LobbyDetailsArray;
        }
        
        private IEnumerator PollLobbiesRoutineWithConnection()
        {
            yield return LocalUser.Get(out var localUser);

            // 🔹 Количество проходок поиска (можно вынести в настройки LobbyVariables)
            int maxSearchAttempts = ClientDataStorage.UserData.IsAdminRole ? 1 : 3;
            float waitBetweenAttempts = LobbyVariables.Instance.pollLobbiesInterval;

            while (enabled)
            {
                bool lobbyFound = false;

                for (int attempt = 0; attempt < maxSearchAttempts; attempt++)
                {
                    LobbyVariables.Instance.lobbyPopupUI.Show(
                        $"Searching lobby...", "", 10);

                    // 🔹 Запрос поиска лобби
                    yield return LobbySearchLobbies.Run(out var searchLobbies, localUser.Id);

                    // 🔹 Сохраняем результаты поиска (для UI/отладки)
                    LobbyVariables.Instance.searchResults = searchLobbies.LobbyDetailsArray;

                    var lobbyList = searchLobbies.LobbyDetailsArray.ToList();

                    // Если нашли хотя бы одно подходящее лобби — прекращаем поиск
                    if (lobbyList.Count > 0)
                    {
                        lobbyFound = true;
                        ChoiceAndJoinLobby(lobbyList);
                        break;
                    }

                    // ⏳ Пауза между попытками поиска
                    yield return new WaitForSeconds(waitBetweenAttempts);
                }

                if (!lobbyFound)
                {
                    // ❗ За N попыток не найдено ни одного лобби → создаем свое
                    LobbyVariables.Instance.lobbyPopupUI.Show("Creating lobby...", "", 10);
                    StartCoroutine(OnHobbyLobbyClickedRoutine());
                }

                // 🔁 Интервал до следующего полного цикла поиска/создания
                yield return new WaitForSeconds(waitBetweenAttempts);
            }
        }

        private void ChoiceAndJoinLobby(List<LobbyDetails> lobbies)
        {
            var freeSlotsReq = 1;

            var filteredLobby = lobbies.Where(l =>
            {
                global::Code.Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(l, out var info);

                if (info == null)
                    return false;

                var maxMembers = info.Value.MaxMembers;
                var memberCount = global::Code.Network.Lobby.EOSCoroutines.Lobby.GetMembers(l).Count;
                var isHostResult =
                    global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttribute(l, "HOST_IN", out var isHost);
                var isAdminResult =
                    global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttribute(l, "ADMIN_IN", out var isAdmin);
                var isModerResult =
                    global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttribute(l, "MODER_IN", out var isModer);
                var freeSlots = maxMembers - memberCount;

                if (!ClientDataStorage.UserData.IsAdminRole)
                {
                    freeSlotsReq += isHostResult != Result.Success || isHost.Value.Data.Value.Value.AsUtf8 == "FALSE"
                        ? 1
                        : 0;
                    freeSlotsReq += isAdminResult != Result.Success || isAdmin.Value.Data.Value.Value.AsUtf8 == "FALSE"
                        ? 1
                        : 0;
                    freeSlotsReq += isModerResult != Result.Success || isModer.Value.Data.Value.Value.AsUtf8 == "FALSE"
                        ? 1
                        : 0;
                }

                return freeSlots >= freeSlotsReq;
            }).ToList();

            StartCoroutine(filteredLobby.Count > 0
                ? OnJoinLobbyClickedRoutine(filteredLobby.First())
                : OnHobbyLobbyClickedRoutine());
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

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Logging in...", 20);
            yield return LocalUser.Get(out var localUser);
            var localUserId = localUser.Id;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Creating Lobby...", 40);
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
            var currentLobby = new LobbyData { lobbyId = lobbyId, lobbyName = lobbyName, maxPlayers = maxLobbyUsers };
            LobbyVariables.Instance.currentLobby = currentLobby;

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Lobby Name...", 60);
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

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Display Name...", 80);
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME",
                LobbyVariables.Instance.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby member name: {setName.CallbackInfo?.ResultCode}");

            yield return LobbySetMemberAttribute.Run(out var setRole, lobbyId, localUserId, "ROLE",
                ClientDataStorage.UserData.role);
            if (setRole.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby member role: {setRole.CallbackInfo?.ResultCode}");

            yield return LobbySetMemberAttribute.Run(out var setHardScore, lobbyId, localUserId, "HARDWARE_SCORE",
                HardwareScore.GetScore().ToString());
            if (setHardScore.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning(
                    $"[LobbyCode] Failed to update lobby member score: {setHardScore.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Id...", 99);
            yield return LobbyUpdateLobby.Run(out var setId, lobbyId, "HOST_ID",
                localUserId.ToString());

            yield return LobbyUpdateLobby.Run(out var setVersion, lobbyId, "PRODUCT_VERSION",
                Application.version);


            LobbyVariables.Instance.lobbyPopupUI.Show("Hosting Lobby...", "Setting Host Id...", 100);

            if (setId.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to set lobby member host id: {setId.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Hide();

            SetLobbyAttributes(currentLobby, lobbyDetails);

            OnHostConnectionReady();
            StartUpdatingPing(10);

            lobbyDetails.Release();
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

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Please wait...", 20);
            yield return LobbyJoinLobby.Run(out var joinLobby, localUserId, lobbyDetails);
            if (joinLobby.CallbackInfo?.ResultCode != Result.Success)
            {
                yield return LobbyVariables.Instance.lobbyPopupUI.PromptCoroutine(out _, "Error",
                    joinLobby.CallbackInfo?.ResultCode.ToString());
                StartPollingLobbies();
                yield break;
            }

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Getting Lobby Info...", 40);
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

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Getting Lobby Name...", 60);
            var getAttributeResult =
                global::Code.Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobbyDetails, "NAME",
                    out var lobbyNameAttribute);
            if (getAttributeResult != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to get lobby name: {getAttributeResult}");
            LobbyVariables.Instance.hostLobbyName.Value = lobbyNameAttribute?.Data?.Value.AsUtf8;

            var currentLobby = new LobbyData { lobbyId = lobbyId, lobbyName = lobbyNameAttribute?.Data?.Value.AsUtf8, };
            LobbyVariables.Instance.currentLobby = currentLobby;

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Setting Local User Display Name...", 99);
            yield return LobbySetMemberAttribute.Run(out var setName, lobbyId, localUserId, "NAME",
                LobbyVariables.Instance.displayName);
            if (setName.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby member name: {setName.CallbackInfo?.ResultCode}");

            yield return LobbySetMemberAttribute.Run(out var setRole, lobbyId, localUserId, "ROLE",
                ClientDataStorage.UserData.role);
            if (setRole.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning($"[LobbyCode] Failed to update lobby member role: {setRole.CallbackInfo?.ResultCode}");

            yield return LobbySetMemberAttribute.Run(out var setHardScore, lobbyId, localUserId, "HARDWARE_SCORE",
                HardwareScore.GetScore().ToString());
            if (setHardScore.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning(
                    $"[LobbyCode] Failed to update lobby member score: {setHardScore.CallbackInfo?.ResultCode}");

            LobbyVariables.Instance.lobbyPopupUI.Show("Joining Lobby...", "Getting Attributes...", 100);
            SetLobbyAttributes(currentLobby, lobbyDetails);

            LobbyVariables.Instance.lobbyPopupUI.Hide();

            OnClientConnectionReady();

            StartUpdatingPing(10);
        }

        public void UpdateLobbyAttribute(string attr, string value)
        {
            StartCoroutine(UpdateLobbyAttributes(attr, value));
        }

        private IEnumerator UpdateLobbyAttributes(string attr, string value)
        {
            var lobby = LobbyVariables.Instance.currentLobby;
            if (lobby == null) yield break;

            var lobbyId = lobby.lobbyId;

            yield return LobbyUpdateLobby.Run(out var updateLobby, lobbyId, attr, value);
            if (updateLobby.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogWarning(
                    $"[LobbyCode] Failed to update lobby arr {attr}: {updateLobby.CallbackInfo?.ResultCode}");
        }

        private void OnLobbyAttributesUpdated(LobbyUpdateReceivedCallbackInfo e)
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

            var attrKeys = new StringBuilder();
            for (var i = 0; i < attributes.Count; i++)
            {
                attrKeys.Append($"{attributes[i]?.Data?.Key} ");
                currentLobby.attributeKeys[i] = attributes[i]?.Data?.Key;
                currentLobby.attributeValues[i] = attributes[i]?.Data?.Value.AsUtf8;
            }

            Debug.Log(attrKeys);

            var newHostId = currentLobby.Attributes.TryGetValue("HOST_ID", out var newHostIdValue)
                ? newHostIdValue
                : string.Empty;

            if (!string.IsNullOrEmpty(oldHostId) && newHostId != oldHostId)
            {
                // Вызываем событие смены хоста
                Debug.LogWarning($"[LobbyController] Host updated to {newHostId}");
                OnHostChanged?.Invoke(newHostId);
            }

            UpdateMembers();
        }

        private void OnLobbyMemberStatusReceived(LobbyMemberStatusReceivedCallbackInfo arg)
        {
            UpdateMembers();
            CheckNewOwner(arg);
        }

        private async void CheckNewOwner(LobbyMemberStatusReceivedCallbackInfo arg)
        {
            if (arg.CurrentStatus is not LobbyMemberStatus.Promoted ||
                arg.TargetUserId.ToString() != LobbyVariables.Instance.ProductUserId.ToString()) return;
            await Task.Delay(2_500);
            Debug.Log($"[HostMigration] I am new owner");
            if (!LobbyVariables.Instance.currentLobby.Attributes.TryGetValue("PROMOTE_MANUALLY",
                    out var isPromoteManually)
                || isPromoteManually == "FALSE")
                SelectNewHostAndPromote(true);
            else
                PromoteHandle();
        }

        private void PromoteHandle()
        {
            Debug.Log($"[HostMigration] I manually promoted");
            StartCoroutine(UpdateLobbyAttributes("PROMOTE_MANUALLY", "FALSE"));
            OnCurrentHostDisconnected?.Invoke(LobbyVariables.Instance.ProductUserId.ToString());
        }

        private void OnMembersUpdate(LobbyMemberUpdateReceivedCallbackInfo e)
        {
            UpdateMembers();

            if (!InstanceFinder.ServerManager.Started)
                return;

            var members = LobbyVariables.Instance.currentLobby.lobbyMembers;

            var isHost = members.FirstOrDefault(m =>
                m.Attributes.TryGetValue("ROLE", out var roleValue) && roleValue == "host") != null;
            UpdateLobbyAttribute("HOST_IN", isHost ? "TRUE" : "FALSE");

            var isAdmin = members.FirstOrDefault(m =>
                m.Attributes.TryGetValue("ROLE", out var roleValue) && roleValue == "admin") != null;
            UpdateLobbyAttribute("ADMIN_IN", isAdmin ? "TRUE" : "FALSE");

            var isModer = members.FirstOrDefault(m =>
                m.Attributes.TryGetValue("ROLE", out var roleValue) && roleValue == "moderator") != null;
            UpdateLobbyAttribute("MODER_IN", isModer ? "TRUE" : "FALSE");
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
        public void JoinLobbyById(string lobbyId)
        {
            StartCoroutine(OnJoinLobbyByHostIdRoutine(lobbyId));
        }

        private IEnumerator OnJoinLobbyByHostIdRoutine(string id)
        {
            yield return LocalUser.Get(out var localUser);
            yield return LobbySearchLobbies.Run(out var searchLobbies, localUser.Id);

            var lobby = searchLobbies.LobbyDetailsArray.FirstOrDefault(l =>
                {
                    Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(l, out var info);
                    return info.HasValue && info.Value.LobbyId == id;
                });

            if (lobby != null)
                StartCoroutine(OnJoinLobbyClickedRoutine(lobby));
            else
                Debug.LogWarning($"[LobbyController] Lobby with HOST_ID {id} not found.");
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

        public void UpdateHost(string newHostId)
        {
            EOS.GetManager()?.StartCoroutine(UpdateHostCoroutine(newHostId));
        }

        private IEnumerator UpdateHostCoroutine(string newHostId)
        {
            yield return LobbyUpdateLobby.Run(out var updateLobbyHostId, LobbyVariables.Instance.currentLobby.lobbyId,
                "HOST_ID", newHostId);
            Debug.Log(newHostId);
            if (updateLobbyHostId.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogError(
                    $"[HostMigrator] Failed to set lobby member host id: {updateLobbyHostId.CallbackInfo?.ResultCode}");
        }

        public void LeaveLobby()
        {
            EOS.GetManager()?.StartCoroutine(LeaveLobbyRoutine());
        }

        public void SelectNewHostAndPromote(bool includeMe = false)
        {
            Debug.Log($"[HostMigration] I Select new host");
            var newHostId = NewHostAutoSelector.GetNewHostIdAuto(includeMe);
            Debug.Log($"[HostMigration] New host ID: {newHostId}");
            if (newHostId != LobbyVariables.Instance.productUserId)
                Promote(newHostId);
            else
                PromoteHandle();
        }

        public void Promote(string newHostId)
        {
            StartCoroutine(UpdateLobbyAttributes("PROMOTE_MANUALLY", "TRUE"));
            StartCoroutine(PromoteLobbyRoutine(newHostId));
        }

        private IEnumerator PromoteLobbyRoutine(string newHostId)
        {
            var lobbyId = LobbyVariables.Instance.currentLobby.lobbyId;
            if (string.IsNullOrEmpty(lobbyId))
                yield break;

            var clientData = ClientDataStorage.UserData.username;

            yield return LobbyPromoteHost.Run(out var lobbyPromoteHost, lobbyId, newHostId, clientData);
            if (lobbyPromoteHost.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogError(
                    $"[LobbyController] Failed to promote lobby: {lobbyPromoteHost.CallbackInfo?.ResultCode}");
            else
                Debug.Log($"[LobbyController] Successfully promote lobby new owner is {newHostId}");
        }

        private IEnumerator LeaveLobbyRoutine()
        {
            var lobbyId = LobbyVariables.Instance.currentLobby.lobbyId;
            if (string.IsNullOrEmpty(lobbyId))
                yield break;

            var userID = LobbyVariables.Instance.ProductUserId;
            yield return LobbyLeaveLobby.Run(out var leaveLobbyResult, lobbyId, userID);
            if (leaveLobbyResult.CallbackInfo?.ResultCode != Result.Success)
                Debug.LogError(
                    $"[LobbyController] Failed to leave lobby: {leaveLobbyResult.CallbackInfo?.ResultCode}]");
            else
                Debug.Log($"[LobbyController] Successfully leave lobby {lobbyId}");
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