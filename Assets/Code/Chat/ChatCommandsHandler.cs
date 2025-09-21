using System.Linq;
using System.Text;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network.Lobby;
using Code.Network.Player;
using Code.Player;
using Code.Scene.SceneObjectControl;
using Dissonance;
using FishNet;
using FishNet.Component.Animating;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Object;
using Proyecto26;
using TankAndHealerStudioAssets;
using UnityEngine;

namespace Code.Chat
{
    public class ChatCommandsHandler : NetworkBehaviour
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
                chatController.SendSystemMessage("You can't kick other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }

            Kick_ServerRPC(ClientManager.Connection, username);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Kick_ServerRPC(NetworkConnection sender, string username)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                KickCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            if (PlayerSpawner.SpawnedPlayerData_Server.TryGetValue(connection, out var playerData)
                && playerData.IsAdminRole)
            {
                KickCallback_Rpc(sender, $"User {username} cannot be kicked", false);
                return;
            }

            // ServerManager.Kick(connection, KickReason.Unset);
            KickCallback_Rpc(null, $"User {username} was kicked", true, connection);
        }

        [TargetRpc, ObserversRpc]
        private void KickCallback_Rpc(NetworkConnection target, string message, bool success,
            NetworkConnection kickedConnection = null)
        {
            chatController.SendSystemMessage(message,
                !success ? UltimateChatBoxStyles.errorMessage : UltimateChatBoxStyles.noticeMessage);
            if (kickedConnection != null && kickedConnection == ClientManager.Connection)
            {
                PlayerPrefs.DeleteKey("auth_accessToken");
                LobbyDisconnector.Disconnect(true, "You was kicked / baned");
            }
        }

        #endregion
        
        #region Ban

        public void BanUser(string usernameTime)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                chatController.SendSystemMessage($"You can't ban users", UltimateChatBoxStyles.errorMessage);
                return;
            }

            var parametres = usernameTime.Split(' ');
            var username = parametres[0];
            var time = parametres.Length > 1 ? int.Parse(parametres[1]) : 0;

            if (LobbyVariables.Instance.currentLobby.lobbyMembers.FirstOrDefault(m => m.displayName == username) ==
                null)
            {
                chatController.SendSystemMessage($"User not found in lobby", UltimateChatBoxStyles.errorMessage);
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
                    chatController.SendSystemMessage(banResponse.Error, UltimateChatBoxStyles.errorMessage);
                    return;
                }

                var responseData = JsonUtility.FromJson<SuccessResponse<Empty>>(banResponse.Text);
                if (!responseData.success)
                {
                    chatController.SendSystemMessage(responseData.detail, UltimateChatBoxStyles.errorMessage);
                    return;
                }
                
                chatController.SendSystemMessage($"User {username} was banned", UltimateChatBoxStyles.noticeMessage);
                
                Kick(username);
            });
        }

        public void UnbanUser(string username)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                chatController.SendSystemMessage($"You can't unban users", UltimateChatBoxStyles.errorMessage);
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
                    chatController.SendSystemMessage(unbanResponse.Error, UltimateChatBoxStyles.errorMessage);
                    return;
                }

                var responseData = JsonUtility.FromJson<SuccessResponse<Empty>>(unbanResponse.Text);
                if (!responseData.success)
                {
                    chatController.SendSystemMessage(responseData.detail, UltimateChatBoxStyles.errorMessage);
                    return;
                }
                
                chatController.SendSystemMessage($"User {username} was unbanned", UltimateChatBoxStyles.noticeMessage);
                
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
                chatController.SendSystemMessage("You can't mute other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }

            Mute_ServerRpc(ClientManager.Connection, username, muteChat, muteVoice);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Mute_ServerRpc(NetworkConnection sender, string username, bool muteChat, bool muteVoice)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                MuteCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            if (PlayerSpawner.SpawnedPlayerData_Server.TryGetValue(connection, out var playerData)
                && playerData.IsAdminRole)
            {
                MuteCallback_Rpc(sender, $"User {username} cannot be muted", false);
                return;
            }

            MuteCallback_Rpc(null, $"User {username} was muted", true, muteChat, muteVoice, connection);
        }

        [ObserversRpc, TargetRpc]
        private void MuteCallback_Rpc(NetworkConnection target, string message, bool success, bool muteChat = false,
            bool muteVoice = false, NetworkConnection muteConnection = null)
        {
            if (!success)
            {
                chatController.SendSystemMessage(message, UltimateChatBoxStyles.errorMessage);
                return;
            }

            chatController.SendSystemMessage(message, UltimateChatBoxStyles.noticeMessage);

            if (muteConnection != ClientManager.Connection)
                return;

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
                chatController.SendSystemMessage("You can't unmute other users.",
                    UltimateChatBoxStyles.errorMessage);
                return;
            }

            Unmute_ServerRpc(ClientManager.Connection, username, unmuteChat, unmuteVoice);
        }

        [ServerRpc(RequireOwnership = false)]
        private void Unmute_ServerRpc(NetworkConnection sender, string username, bool unmuteChat, bool unmuteVoice)
        {
            if (!PlayerSpawner.NameConnectionsData_Server.TryGetValue(username, out var connection))
            {
                UnmuteCallback_Rpc(sender, $"User {username} not found", false);
                return;
            }

            if (PlayerSpawner.SpawnedPlayerData_Server.TryGetValue(connection, out var playerData)
                && playerData.IsAdminRole)
            {
                UnmuteCallback_Rpc(sender, $"User {username} cannot be unmuted", false);
                return;
            }

            UnmuteCallback_Rpc(null, $"User {username} was unmuted", true, unmuteChat, unmuteVoice, connection);
        }

        [ObserversRpc, TargetRpc]
        private void UnmuteCallback_Rpc(NetworkConnection target, string message, bool success, bool unmuteChat = false,
            bool unmuteVoice = false, NetworkConnection muteConnection = null)
        {
            if (!success)
            {
                chatController.SendSystemMessage(message, UltimateChatBoxStyles.errorMessage);
                return;
            }

            chatController.SendSystemMessage(message, UltimateChatBoxStyles.noticeMessage);

            if (muteConnection != ClientManager.Connection)
                return;

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
                chatController.SendSystemMessage($"User {promotedUserName} not found",
                    UltimateChatBoxStyles.errorMessage);
                return;
            }

            var userId = userMemberData.productUserId;

            if (ServerManager.Started)
                FindAnyObjectByType<LobbyController>().Promote(userId);
            else if (ClientDataStorage.UserData.IsAdminRole)
                Promote_ServerRpc(userId);
            else
                chatController.SendSystemMessage("You can't promote members", UltimateChatBoxStyles.errorMessage);
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
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                chatController.SendSystemMessage("You can't reset slots", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            if(!int.TryParse(idString, out var id))
            {
                chatController.SendSystemMessage("Invalid param", UltimateChatBoxStyles.errorMessage);
                return;
            }

            if (SlotMachineInteractable.FindById(id) == null)
            {
                chatController.SendSystemMessage("Slot not found", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            chatController.SendSystemMessage($"Request reset slot {id}", UltimateChatBoxStyles.noticeMessage);
            ResetSlot_ServerRpc(id);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ResetSlot_ServerRpc(int id)
        {
            var slot = SlotMachineInteractable.FindById(id);
            var compositeInteractionComponent = slot.GetComponentInParent<CompositeInteractable>();
            if(compositeInteractionComponent != null)
                compositeInteractionComponent.ReleaseInteractable();
        }

        #endregion

        #region Room

        public void NewRoom(string roomName)
        {
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                chatController.SendSystemMessage("You can not create new rooms", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            LobbyDisconnector.Disconnect();
            var lobbyController = FindAnyObjectByType<LobbyController>();
            lobbyController.CreateLobbyManual(roomName, 64);
        }

        public void SceneControl(string args)
        {
            Debug.Log($"[Command] SceneControl: {args}");
            if (!ClientDataStorage.UserData.IsAdminRole)
            {
                chatController.SendSystemMessage("You can not control scene objects", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            var arguments = args.Split(' ');
            if (arguments.Length != 2)
            {
                chatController.SendSystemMessage("Command must contain 2 args: object name and action", UltimateChatBoxStyles.errorMessage);
                return;
            }

            if (!sceneObjectController.IsObjectExist(arguments[0]))
            {
                chatController.SendSystemMessage($"Object \"{arguments[0]}\" not found", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            sceneObjectController.MakeAction(arguments[0], arguments[1]);
        }

        #endregion
    }
}