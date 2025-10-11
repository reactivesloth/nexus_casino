using System.Collections;
using System.Linq;
using System.Text;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network.Lobby;
using Code.Network.Player;
using Code.Scene.SceneObjectControl;
using Dissonance;
using Epic.OnlineServices;
using FishNet.Connection;
using FishNet.Object;
using Proyecto26;
using TankAndHealerStudioAssets;
using UnityEngine;

namespace Code.Chat
{
    public class AdminPanelHandler : NetworkBehaviour
    {
        [SerializeField] private ChatController chatController;
        [SerializeField, TextArea] private string helpText;

        [SerializeField] private SceneObjectController sceneObjectController;

        protected override void OnValidate()
        {
            base.OnValidate();
            chatController ??= GetComponent<ChatController>();
            sceneObjectController ??= FindAnyObjectByType<SceneObjectController>();
        }

        public void Help()
        {
            if (chatController == null || chatController.CurrentChatBox == null) return;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(helpText))
                sb.AppendLine($"<color=yellow>{helpText}</color>");

            // без LINQ/foreach alloc
            var dict = chatController.CommandsDictionary;
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    var cmd = kv.Value;
                    if (cmd != null)
                        sb.AppendLine(cmd.commandValue + " - " + cmd.description);
                }
            }

            chatController.CurrentChatBox.RegisterChat(chatController.SystemName, sb.ToString());
        }

        #region Kick

        public void Kick(string username)
        {
            // if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                CommandCallback("You can't kick other users.", false);
                return;
            }

            Kick_ServerRPC(ClientManager.Connection, username);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Kick_ServerRPC(NetworkConnection sender, string username)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            if (PlayerSpawner.SpawnedPlayerData_Server.TryGetValue(connection, out var playerData)
                && playerData.IsAdminRole)
            {
                CommandCallback_Rpc(sender, $"User {username} cannot be kicked", false);
                return;
            }

            CommandCallback_Rpc(null, $"User {username} was kicked", true);
            Kick_TargetRpc(connection);
        }

        [TargetRpc]
        private void Kick_TargetRpc(NetworkConnection target)
        {
            PlayerPrefs.DeleteKey("auth_accessToken");
            LobbyDisconnector.Disconnect(true, "You was kicked / baned");
        }

        #endregion

        #region Ban

        public void BanUser(string usernameTime)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback($"You can't ban users", false);
                return;
            }

            var parametres = usernameTime.Split(' ');
            var username = parametres[0];
            var time = parametres.Length > 1 ? int.Parse(parametres[1]) : 0;

            if (LobbyVariables.Instance.currentLobby.lobbyMembers.FirstOrDefault(m => m.displayName == username) ==
                null)
            {
                CommandCallback($"User not found in lobby", false);
                return;
            }

            var banRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetBanUrl(),
                Body = new BanData
                {
                    username = username,
                    timeout_minutes = time
                },
                Headers = ClientDataStorage.GetJwtHeader(),
            };

            RestClient.Post(banRequest).Then(banResponse =>
            {
                if (banResponse.StatusCode != 200)
                {
                    CommandCallback(banResponse.Error, false);
                    return;
                }

                var responseData = JsonUtility.FromJson<SuccessResponse<Empty>>(banResponse.Text);
                if (!responseData.success)
                {
                    CommandCallback(responseData.detail, false);
                    return;
                }

                CommandCallback($"User {username} was banned", true);

                Kick(username);
            });
        }

        public void UnbanUser(string username)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback($"You can't unban users", false);
                return;
            }

            var unbanRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetUnbanUrl(),
                Body = new BanData
                {
                    username = username
                },
                Headers = ClientDataStorage.GetJwtHeader(),
            };

            RestClient.Post(unbanRequest).Then(unbanResponse =>
            {
                if (unbanResponse.StatusCode != 200)
                {
                    CommandCallback(unbanResponse.Error, false);
                    return;
                }

                var responseData = JsonUtility.FromJson<SuccessResponse<Empty>>(unbanResponse.Text);
                if (!responseData.success)
                {
                    CommandCallback(responseData.detail, false);
                    return;
                }

                CommandCallback($"User {username} was unbanned", true);
            });
        }

        #endregion

        #region Communacations Commands

        public void Mute(string username) => Mute(username, true, true);

        public void MuteChat(string username) => Mute(username, true, false);

        public void MuteVoice(string username) => Mute(username, false, true);

        private void Mute(string username, bool muteChat, bool muteVoice)
        {
            // if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                CommandCallback("You can't mute other users.", false);
                return;
            }

            Mute_ServerRpc(ClientManager.Connection, username, muteChat, muteVoice);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Mute_ServerRpc(NetworkConnection sender, string username, bool muteChat, bool muteVoice)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            if (PlayerSpawner.SpawnedPlayerData_Server.TryGetValue(connection, out var playerData)
                && playerData.IsAdminRole)
            {
                CommandCallback_Rpc(sender, $"User {username} cannot be muted", false);
                return;
            }

            CommandCallback_Rpc(null, $"User {username} was muted", true);
            Mute_TargetRpc(connection, muteChat, muteVoice);
        }

        [TargetRpc]
        private void Mute_TargetRpc(NetworkConnection target, bool muteChat = false, bool muteVoice = false)
        {
            if (muteChat)
                chatController.IsMuted = true;
            if (muteVoice)
                FindAnyObjectByType<VoiceBroadcastTrigger>().IsMuted = true;
        }

        public void Unmute(string username) => Unmute(username, true, true);

        public void UnmuteChat(string username) => Unmute(username, true, false);

        public void UnmuteVoice(string username) => Unmute(username, false, true);

        private void Unmute(string username, bool unmuteChat, bool unmuteVoice)
        {
            // if (false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                CommandCallback("You can't unmute other users.",
                    false);
                return;
            }

            Unmute_ServerRpc(ClientManager.Connection, username, unmuteChat, unmuteVoice);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Unmute_ServerRpc(NetworkConnection sender, string username, bool unmuteChat, bool unmuteVoice)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            if (PlayerSpawner.SpawnedPlayerData_Server.TryGetValue(connection, out var playerData)
                && playerData.IsAdminRole)
            {
                CommandCallback_Rpc(sender, $"User {username} cannot be unmuted", false);
                return;
            }

            CommandCallback_Rpc(null, $"User {username} was unmuted", true);
            UnmuteCallback_Rpc(connection, unmuteChat, unmuteVoice);
        }

        [TargetRpc]
        private void UnmuteCallback_Rpc(NetworkConnection target, bool unmuteChat = false, bool unmuteVoice = false)
        {
            if (unmuteChat)
                chatController.IsMuted = false;
            if (unmuteVoice)
                FindAnyObjectByType<VoiceBroadcastTrigger>().IsMuted = false;
        }

        #endregion

        #region Promote

        public void PromoteMember(string promotedUserName)
        {
            var userMemberData = LobbyVariables.Instance.currentLobby.lobbyMembers.FirstOrDefault(m =>
            {
                if (!m.Attributes.TryGetValue("NAME", out var memberName))
                    return false;
                return memberName == promotedUserName;
            });

            if (userMemberData == null)
            {
                CommandCallback($"User {promotedUserName} not found",
                    false);
                return;
            }

            var userId = userMemberData.productUserId;

            if (ServerManager.Started)
                FindAnyObjectByType<LobbyController>().Promote(userId);
            else if (ClientDataStorage.UserData.IsAdminRole)
                Promote_ServerRpc(userId);
            else
                CommandCallback("You can't promote members", false);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Promote_ServerRpc(string userId)
        {
            FindAnyObjectByType<LobbyController>().Promote(userId);
        }

        #endregion

        #region Slots

        public void ResetSlot(string idString)
        {
            if (!int.TryParse(idString, out var id))
            {
                CommandCallback("Invalid param", false);
                return;
            }

            ResetSlot(id);
        }

        public void ResetSlot(int id)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback("You can't reset slots", false);
                return;
            }

            if (SlotMachineInteractable.FindById(id) == null)
            {
                CommandCallback("Slot not found", false);
                return;
            }

            CommandCallback($"Request reset slot {id}", true);
            ResetSlot_ServerRpc(id);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ResetSlot_ServerRpc(int id)
        {
            var slot = SlotMachineInteractable.FindById(id);
            var compositeInteractionComponent = slot.GetComponentInParent<CompositeInteractable>();
            if (compositeInteractionComponent != null)
                compositeInteractionComponent.ReleaseInteractable();
        }

        #endregion

        #region Lobbies

        public void NewRoomHandle(string argsString)
        {
            var args = argsString.Split(' ');
            var argsLength = args.Length;
            switch (argsLength)
            {
                case 1:
                    NewRoomHandle(args[0], false, null);
                    break;
                case 2:
                    if(args[1] == "-c")
                        NewRoomHandle(args[0], true, null);
                    else
                        NewRoomHandle(args[0], false, args[1]);
                    break;
                case 3:
                    NewRoomHandle(args[0], args[2] == "-c", args[1]);
                    break;
            }
        }

        public void NewRoomHandle(string roomName, bool isPrivate, string hostName)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback("You can not create new rooms", false);
                return;
            }

            if (hostName == null)
                CreateRoom(roomName, isPrivate);
            else
                CreateRoom_ServerRpc(roomName, isPrivate, hostName, ClientManager.Connection);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CreateRoom_ServerRpc(string roomName, bool isPrivate, string hostName, NetworkConnection sender)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(hostName, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {hostName} not found", false);
                return;
            }
            
            CreateRoom_TargetRpc(connection, roomName, isPrivate);
        }

        [TargetRpc]
        private void CreateRoom_TargetRpc(NetworkConnection target, string roomName, bool isPrivate) =>
            CreateRoom(roomName, isPrivate);

        private void CreateRoom(string roomName, bool isPrivate)
        {
            LobbyDisconnector.Disconnect();
            var lobbyController = FindAnyObjectByType<LobbyController>();
            lobbyController.CreateLobbyManual(roomName, 64, isPrivate: isPrivate);
        }
        
        public void GetRooms()
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback("You can not get rooms", false);
                return;
            }

            StartCoroutine(GetRoomsCoroutine());
        }

        private IEnumerator GetRoomsCoroutine()
        {
            var lobbyController = FindAnyObjectByType<LobbyController>();
            yield return StartCoroutine(lobbyController.PollLobbiesRoutine());
            var lobbies = lobbyController.GetAllLobbies();

            var textToInput = new StringBuilder();
            foreach (var lobby in lobbies)
            {
                Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(lobby, out var info);
                if (!info.HasValue)
                    continue;

                var nameResult = Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, "NAME", out var nameAttr);

                var lobbyId = info.Value.LobbyId;
                var lobbyName = nameResult == Result.Success
                    ? nameAttr.Value.Data.Value.Value.AsUtf8.ToString()
                    : string.Empty;
                var maxPlayersCount = info.Value.MaxMembers;
                var currentPlayersCount = Network.Lobby.EOSCoroutines.Lobby.GetMembers(lobby).Count;

                textToInput.Append(
                    $"<color=red>{lobbyId}</color> {lobbyName} [{currentPlayersCount}/{maxPlayersCount}] \n");
            }

            CommandCallback(textToInput.ToString(), true);
        }

        public void MoveUserToRoom(string args)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback("You can not move users", false);
                return;
            }

            var argsArray = args.Split(' ');

            if (argsArray.Length != 2)
            {
                CommandCallback("Invalid params", false);
                return;
            }

            MoveUserServerRpc(ClientManager.Connection, argsArray[0], argsArray[1]);
        }

        [ServerRpc(RequireOwnership = false)]
        private void MoveUserServerRpc(NetworkConnection sender, string username, string lobbyName)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            StartCoroutine(MoveUserToRoomCoroutine(sender, connection, username, lobbyName));
        }

        private IEnumerator MoveUserToRoomCoroutine(NetworkConnection sender, NetworkConnection target, string username,
            string lobbyName)
        {
            var lobbyController = FindAnyObjectByType<LobbyController>();
            yield return StartCoroutine(lobbyController.PollLobbiesRoutine());
            var lobbies = lobbyController.GetAllLobbies().ToList();

            var lobby = lobbies.Find(l =>
            {
                var nameResult = Network.Lobby.EOSCoroutines.Lobby.GetAttribute(l, "NAME", out var nameAttr);

                var findLobbyName = nameResult == Result.Success
                    ? nameAttr.Value.Data.Value.Value.AsUtf8.ToString()
                    : string.Empty;

                return findLobbyName == lobbyName;
            });

            if (lobby == null)
            {
                CommandCallback_Rpc(sender, $"Lobby {lobbyName} not found", false);
                yield break;
            }

            Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(lobby, out var info);
            if (!info.HasValue)
            {
                CommandCallback_Rpc(sender, $"Unknown error", false);
                yield break;
            }

            var lobbyId = info.Value.LobbyId;

            MoveUserTargetRpc(target, lobbyId);
            CommandCallback_Rpc(sender, $"Moved {username} to {lobbyId}", true);
        }

        [TargetRpc]
        private void MoveUserTargetRpc(NetworkConnection target, string lobbyId)
        {
            var lobbyController = FindAnyObjectByType<LobbyController>();

            CommandCallback("You moved to another lobby", true);

            LobbyDisconnector.Disconnect();
            lobbyController.JoinLobbyById(lobbyId);
        }

        #endregion

        #region Scene

        public void SceneControl(string args)
        {
            var arguments = args.Split(' ');
            if (arguments.Length != 2)
            {
                CommandCallback("Command must contain 2 args: object name and action", false);
                return;
            }

            SceneControl(arguments[0], arguments[1]);
        }

        public void SceneControl(string objectName, string newState)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback("You can not control scene objects", false);
                return;
            }

            var states = sceneObjectController.GetStatesByName(objectName);

            if (states == null)
            {
                CommandCallback($"Object \"{objectName}\" not found", false);
                return;
            }

            if (!states.Contains(newState))
            {
                CommandCallback($"State \"{newState}\" not found on object \"{objectName}\" not found", false);
                return;
            }

            sceneObjectController.MakeAction(objectName, newState);
        }

        #endregion

        #region Commons

        [TargetRpc, ObserversRpc]
        private void CommandCallback_Rpc(NetworkConnection target, string message, bool success) =>
            CommandCallback(message, success);

        private void CommandCallback(string message, bool success)
        {
            chatController.SendSystemMessage(message,
                !success ? UltimateChatBoxStyles.errorMessage : UltimateChatBoxStyles.noticeMessage);
        }

        #endregion
    }
}