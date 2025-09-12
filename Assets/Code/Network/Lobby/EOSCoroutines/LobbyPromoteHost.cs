using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class LobbyPromoteHost
    {
        public PromoteMemberCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(out LobbyPromoteHost lobbyPromoteHost, string lobbyId, Utf8String target,
            object clientData = null, float timeout = 30f)
        {
            lobbyPromoteHost = new LobbyPromoteHost();
            return EOS.GetManager().StartCoroutine(lobbyPromoteHost.PromoteMember(lobbyId, target, clientData, timeout));
        }

        private IEnumerator PromoteMember(string lobbyId, Utf8String targetUserId, object clientData, float timeout)
        {
            var lobbyInterface = EOS.GetPlatformInterface().GetLobbyInterface();
            var promoteMemberOptions = new PromoteMemberOptions
            {
                LobbyId = lobbyId,
                LocalUserId = EOS.LocalProductUserId,
                TargetUserId = ProductUserId.FromString(targetUserId)
            };
            lobbyInterface.PromoteMember(ref promoteMemberOptions, clientData,
                (ref PromoteMemberCallbackInfo data) => CallbackInfo = data);

            yield return new WaitUntilOrTimeout(() => CallbackInfo.HasValue, timeout,
                () => CallbackInfo = new PromoteMemberCallbackInfo { ResultCode = Result.TimedOut });
        }
    }
}