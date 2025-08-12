﻿using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class LobbyCreateLobby
    {
        public CreateLobbyCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(
            out LobbyCreateLobby lobbyCreateLobby,
            ProductUserId localUserId,
            uint maxLobbyMembers,
            string bucketId = "MyBucket",
            LobbyPermissionLevel permissionLevel = LobbyPermissionLevel.Publicadvertised,
            float timeout = 30f)
        {
            lobbyCreateLobby = new LobbyCreateLobby();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[LobbyCreateLobby] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(lobbyCreateLobby.CreateLobby(localUserId, maxLobbyMembers, bucketId, permissionLevel, timeout));
        }

        private IEnumerator CreateLobby(ProductUserId localUserId, uint maxLobbyMembers, string bucketId, LobbyPermissionLevel permissionLevel, float timeout)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null)
            {
                CallbackInfo = new CreateLobbyCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var lobbyInterface = platform.GetLobbyInterface();
            var createLobbyOptions = new CreateLobbyOptions
            {
                LocalUserId = localUserId,
                MaxLobbyMembers = maxLobbyMembers,
                PermissionLevel = permissionLevel,
                BucketId = bucketId,
            };

            lobbyInterface.CreateLobby(ref createLobbyOptions, null, (ref CreateLobbyCallbackInfo cb) => { CallbackInfo = cb; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                timeout,
                () => CallbackInfo = new CreateLobbyCallbackInfo { ResultCode = Result.TimedOut }
            );
        }
    }
}
