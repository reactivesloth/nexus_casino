using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class LobbyJoinLobby
    {
        public JoinLobbyCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(
            out LobbyJoinLobby lobbyJoinLobby,
            ProductUserId localUserId,
            LobbyDetails lobbyDetails,
            float timeout = 30f)
        {
            lobbyJoinLobby = new LobbyJoinLobby();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[LobbyJoinLobby] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(lobbyJoinLobby.JoinLobby(localUserId, lobbyDetails, timeout));
        }

        private IEnumerator JoinLobby(ProductUserId localUserId, LobbyDetails lobbyDetails, float timeout)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null)
            {
                CallbackInfo = new JoinLobbyCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var lobbyInterface = platform.GetLobbyInterface();
            var joinLobbyOptions = new JoinLobbyOptions
            {
                LobbyDetailsHandle = lobbyDetails,
                LocalUserId = localUserId,
            };

            lobbyInterface.JoinLobby(ref joinLobbyOptions, null, (ref JoinLobbyCallbackInfo cb) => { CallbackInfo = cb; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                timeout,
                () => CallbackInfo = new JoinLobbyCallbackInfo { ResultCode = Result.TimedOut }
            );
        }
    }
}