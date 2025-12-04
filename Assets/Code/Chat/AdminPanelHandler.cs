using System.Linq;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network;
using Code.Network.Player;
using Code.Player;
using Code.Scene.SceneObjectControl;
using Code.UI;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using MetaVoiceChat.NetProviders.FishNet;
using PlayFlow;
using Proyecto26;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Chat
{
    public class AdminPanelHandler : NetworkBehaviour
    {
        [SerializeField] private ChatController chatController;
        [SerializeField, TextArea] private string helpText;

        [FormerlySerializedAs("sceneObjectController")] [SerializeField] private SceneObjectsController sceneObjectsController;
        
        public readonly SyncDictionary<string, MuteStateSync> MutedDictionary = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        [System.Serializable]
        public struct MuteStateSync
        {
            public bool muteChat;
            public bool muteVoice;
        }
        
        protected override void OnValidate()
        {
            base.OnValidate();
            chatController ??= GetComponent<ChatController>();
            sceneObjectsController ??= FindAnyObjectByType<SceneObjectsController>();
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            if(MutedDictionary.TryGetValue(ClientDataStorage.UserData.username, out var mutedStateSync))
                SetMuteState(mutedStateSync.muteChat, mutedStateSync.muteVoice);
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

            PlayFlowLobbyManagerV2.Instance.KickPlayer(username);
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
            PlayFlowFishnet flowFishnet = FindAnyObjectByType<PlayFlowFishnet>(FindObjectsInactive.Include);
            flowFishnet.Disconnect();
            LoadingScreenUI.Instance.LoadScene("Init");
        }

        #endregion

        #region Ban

        public void BanUser(string usernameTime)
        {
            var parametres = usernameTime.Split(' ');
            var username = parametres[0];
            var time = parametres.Length > 1 ? int.Parse(parametres[1]) : 0;
            BanUser(username, time);
        }

        public void BanUser(string username, int time)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback($"You can't ban users", false);
                return;
            }
            
            var banedUser = PlayFlowLobbyManagerV2.Instance.CurrentLobby.players.FirstOrDefault(m => m == username);
            
            if (banedUser == null)
            {
                 CommandCallback($"User not found in lobby", false);
                 return;
            }
            
            Debug.Log($"Banning {username} for {time}");
            
            var banRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetBanUrl(),
                Body = new BanData
                {
                    username = username,
                    timeout_minutes = time
                },
                Headers = ClientDataStorage.GetJwtHeader()
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

                Debug.Log($"BAN {username} for {time}");
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
                Headers = ClientDataStorage.GetJwtHeader()
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
            
            // Получаем текущее состояние мута или создаем новое
            var currentMuteState = MutedDictionary.ContainsKey(username) 
                ? MutedDictionary[username] 
                : new MuteStateSync { muteChat = false, muteVoice = false };

            // Применяем действия мута
            if (muteChat) currentMuteState.muteChat = true;
            if (muteVoice) currentMuteState.muteVoice = true;

            // Обновляем словарь
            MutedDictionary[username] = currentMuteState;

            CommandCallback_Rpc(null, $"User {username} was muted", true);
            Mute_TargetRpc(connection, muteChat, muteVoice);
        }

        [TargetRpc]
        private void Mute_TargetRpc(NetworkConnection target, bool muteChat = false, bool muteVoice = false)
        {
            if (muteChat)
                chatController.IsMuted = true;
            if (muteVoice)
                MetaVCFishNetProvider.LocalPlayerInstance.MetaVc.isInputMutedByServer.Value = true;
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
            
            // Получаем текущее состояние мута или создаем новое
            var currentMuteState = MutedDictionary.ContainsKey(username) 
                ? MutedDictionary[username] 
                : new MuteStateSync { muteChat = false, muteVoice = false };

            // Применяем действия размута
            if (unmuteChat) currentMuteState.muteChat = false;
            if (unmuteVoice) currentMuteState.muteVoice = false;

            // Обновляем словарь
            MutedDictionary[username] = currentMuteState;

            CommandCallback_Rpc(null, $"User {username} was unmuted", true);
            UnmuteCallback_Rpc(connection, unmuteChat, unmuteVoice);
        }

        [TargetRpc]
        private void UnmuteCallback_Rpc(NetworkConnection target, bool unmuteChat = false, bool unmuteVoice = false)
        {
            if (unmuteChat)
                chatController.IsMuted = false;
            if (unmuteVoice)
                MetaVCFishNetProvider.LocalPlayerInstance.MetaVc.isInputMutedByServer.Value = false;
        }

        private void SetMuteState(bool muteChatState, bool muteVoiceState)
        {
            chatController.IsMuted = muteChatState;
            MetaVCFishNetProvider.LocalPlayerInstance.MetaVc.isInputMutedByServer.Value = muteVoiceState;
        }

        public void ToggleOffVoice(string username)
        {
            // if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                CommandCallback("You can't mute other users.", false);
                return;
            }

            //Mute_ServerRpc(ClientManager.Connection, username, muteChat, muteVoice);
            ToggleOffVoce_ServerRpc(ClientManager.Connection, username);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ToggleOffVoce_ServerRpc(NetworkConnection sender, string username)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }
            
            ToggleOffVoice_TargetRpc(connection);
        }

        [TargetRpc]
        private void ToggleOffVoice_TargetRpc(NetworkConnection target)
        {
            FindAnyObjectByType<VoiceChatInputHandler>().VoiceChatHandle(false);
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
                    if (args[1] == "-c")
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

            if (string.IsNullOrEmpty(hostName))
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
            PlayFlowLobbyManagerV2.Instance.LeaveLobby(() =>
            {
                var playFlowFishNet = FindAnyObjectByType<PlayFlowFishnet>();
                if(playFlowFishNet == null)
                    return;
                playFlowFishNet.CreateLobby(roomName, isPrivate);
            });
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

            MoveUserByLobbyName_ServerRpc(ClientManager.Connection, argsArray[0], argsArray[1]);
        }

        public void MoveUserToRoom(string username, string roomId)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                CommandCallback("You can not move users", false);
                return;
            }

            MoveUser_ServerRpc(ClientManager.Connection, username, roomId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void MoveUserByLobbyName_ServerRpc(NetworkConnection sender, string username, string lobbyName)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            MoveUserToRoomCoroutine(sender, connection, username, lobbyName);
        }

        [ServerRpc(RequireOwnership = false)]
        private void MoveUser_ServerRpc(NetworkConnection sender, string username, string lobbyId)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                CommandCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            MoveUserToRoomByIdCoroutine(sender, connection, username, lobbyId);
        }

        private void MoveUserToRoomCoroutine(NetworkConnection sender, NetworkConnection target, string username, string lobbyName)
        {
            MoveUserTargetRpc(target, lobbyName);
            CommandCallback_Rpc(sender, $"Moved {username} to {lobbyName}", true);
        }

        private void MoveUserToRoomByIdCoroutine(NetworkConnection sender, NetworkConnection target,
            string username, string lobbyId)
        {

            MoveUserTargetRpc(target, lobbyId);
            CommandCallback_Rpc(sender, $"Moved {username} to {lobbyId}", true);
        }

        [TargetRpc]
        private void MoveUserTargetRpc(NetworkConnection target, string lobbyId)
        {
            PlayFlowLobbyManagerV2.Instance.LeaveLobby(() =>
            {
                var playFlowFishNet = FindAnyObjectByType<PlayFlowFishnet>();
                if(playFlowFishNet == null)
                    playFlowFishNet.JoinLobby(lobbyId);
            });
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

            var states = sceneObjectsController.GetStatesByName(objectName);

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

            sceneObjectsController.SetState(objectName, newState);
        }

        #endregion

        #region Commons

        [TargetRpc, ObserversRpc]
        private void CommandCallback_Rpc(NetworkConnection target, string message, bool success) =>
            CommandCallback(message, success);

        private void CommandCallback(string message, bool success)
        {
            Debug.Log ($"Message: {message}, success: {success}");
            /*chatController.SendSystemMessage(message,
                !success ? UltimateChatBoxStyles.errorMessage : UltimateChatBoxStyles.noticeMessage);*/
        }

        #endregion
    }
}