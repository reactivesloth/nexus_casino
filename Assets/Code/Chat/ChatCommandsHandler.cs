using System.Text;
using Code.API;
using Code.Network.Lobby;
using Code.Network.Player;
using Code.Player;
using Dissonance;
using FishNet;
using FishNet.Component.Animating;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Object;
using TankAndHealerStudioAssets;
using UnityEngine;

namespace Code.Chat
{
    public class ChatCommandsHandler : NetworkBehaviour
    {
        [SerializeField] private ChatController chatController;
        [SerializeField, TextArea] private string helpText;

        private void OnValidate()
        {
            if (chatController == null)
                chatController = GetComponent<ChatController>();
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
            if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                chatController.SendSystemMessage( "You can't kick other users.", UltimateChatBoxStyles.errorMessage);
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
        private void KickCallback_Rpc(NetworkConnection target, string message, bool success, NetworkConnection kickedConnection = null)
        {
            chatController.SendSystemMessage(message, !success ? UltimateChatBoxStyles.errorMessage : UltimateChatBoxStyles.noticeMessage);
            if(kickedConnection != null && kickedConnection == ClientManager.Connection)
                LobbyAutoDisconnect.Disconnect(true, "You was kicked");
            
        }

        #endregion

        #region Communacations Commands

        public void Mute(string username)
        {
            if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                chatController.SendSystemMessage( "You can't mute other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }

            Mute_ServerRpc(ClientManager.Connection, username, true, true);
        }

        public void MuteChat(string username)
        {
            if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                chatController.SendSystemMessage( "You can't mute other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            Mute_ServerRpc(ClientManager.Connection, username, true, false);
        }
        
        public void MuteVoice(string username)
        {
            if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                chatController.SendSystemMessage( "You can't mute other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            Mute_ServerRpc(ClientManager.Connection, username, false, true);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void Mute_ServerRpc(NetworkConnection sender ,string username, bool muteChat, bool muteVoice)
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
            
            MuteCallback_Rpc(null, $"User {username} was muted", muteChat, muteVoice);
        }

        [ObserversRpc, TargetRpc]
        private void MuteCallback_Rpc(NetworkConnection target, string message, bool success, bool muteChat = false, bool muteVoice = false, NetworkConnection muteConnection = null)
        {
            if (!success)
            {
                chatController.SendSystemMessage(message, UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            chatController.SendSystemMessage(message, UltimateChatBoxStyles.noticeMessage);
            
            if(muteConnection != ClientManager.Connection)
                return;

            if (muteChat)
                chatController.IsMuted = true;
            
            if(muteVoice)
                FindAnyObjectByType<VoiceBroadcastTrigger>().IsMuted = true;
        }
        
        public void Unmute(string username)
        {
            if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                chatController.SendSystemMessage( "You can't unmute other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }

            Unmute_ServerRpc(ClientManager.Connection, username, true, true);
        }

        public void UnmuteChat(string username)
        {
            if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                chatController.SendSystemMessage( "You can't unmute other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            Unmute_ServerRpc(ClientManager.Connection, username, true, false);
        }
        
        public void UnmuteVoice(string username)
        {
            if(false)
            if (!ClientDataStorage.UserData.IsAdminRole) //TODO 
            {
                chatController.SendSystemMessage( "You can't unmute other users.", UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            Unmute_ServerRpc(ClientManager.Connection, username, false, true);
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void Unmute_ServerRpc(NetworkConnection sender ,string username, bool muteChat, bool muteVoice)
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
            
            UnmuteCallback_Rpc(null, $"User {username} was unmuted", muteChat, muteVoice);
        }

        [ObserversRpc, TargetRpc]
        private void UnmuteCallback_Rpc(NetworkConnection target, string message, bool success, bool muteChat = false, bool muteVoice = false, NetworkConnection muteConnection = null)
        {
            if (!success)
            {
                chatController.SendSystemMessage(message, UltimateChatBoxStyles.errorMessage);
                return;
            }
            
            chatController.SendSystemMessage(message, UltimateChatBoxStyles.noticeMessage);
            
            if(muteConnection != ClientManager.Connection)
                return;

            if (muteChat)
                chatController.IsMuted = false;
            
            if(muteVoice)
                FindAnyObjectByType<VoiceBroadcastTrigger>().IsMuted = false;
        }
        
        #endregion
        
    }
}