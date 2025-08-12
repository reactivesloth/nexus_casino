﻿using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class Lobby
    {
        public static Result GetLobbyDetails(out LobbyDetails lobbyDetailsHandle, Utf8String lobbyId, ProductUserId localUserId)
        {
            var opt = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = lobbyId,
                LocalUserId = localUserId,
            };
            var lobbyInterface = EOS.GetPlatformInterface()?.GetLobbyInterface();
            if (lobbyInterface == null)
            {
                lobbyDetailsHandle = null;
                return Result.UnexpectedError;
            }
            return lobbyInterface.CopyLobbyDetailsHandle(ref opt, out lobbyDetailsHandle);
        }

        public static Result GetLobbyInfo(LobbyDetails lobbyDetail, out LobbyDetailsInfo? lobbyInfo)
        {
            var opt = new LobbyDetailsCopyInfoOptions();
            return lobbyDetail != null
                ? lobbyDetail.CopyInfo(ref opt, out lobbyInfo)
                : (lobbyInfo = null, Result.UnexpectedError).Item2;
        }

        public static Result GetAttribute(LobbyDetails lobbyDetail, string attrKey, out Attribute? lobbyAttribute)
        {
            var opt = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = attrKey };
            return lobbyDetail != null
                ? lobbyDetail.CopyAttributeByKey(ref opt, out lobbyAttribute)
                : (lobbyAttribute = null, Result.UnexpectedError).Item2;
        }

        public static Result GetMemberAttribute(LobbyDetails lobbyDetail, ProductUserId productUserId, string attrKey, out Attribute? lobbyAttribute)
        {
            var opt = new LobbyDetailsCopyMemberAttributeByKeyOptions
            {
                AttrKey = attrKey,
                TargetUserId = productUserId
            };
            return lobbyDetail != null
                ? lobbyDetail.CopyMemberAttributeByKey(ref opt, out lobbyAttribute)
                : (lobbyAttribute = null, Result.UnexpectedError).Item2;
        }

        public static List<ProductUserId> GetMembers(LobbyDetails lobbyDetails)
        {
            var list = new List<ProductUserId>();
            if (lobbyDetails == null) return list;

            var countOpt = new LobbyDetailsGetMemberCountOptions();
            uint count = lobbyDetails.GetMemberCount(ref countOpt);

            for (uint i = 0; i < count; i++)
            {
                var byIdx = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                var member = lobbyDetails.GetMemberByIndex(ref byIdx);
                if (member != null)
                    list.Add(member);
            }

            return list;
        }

        public static List<Attribute?> GetMemberAttributes(LobbyDetails lobbyDetails, ProductUserId targetUserId)
        {
            var list = new List<Attribute?>();
            if (lobbyDetails == null || targetUserId == null) return list;

            var countOpt = new LobbyDetailsGetMemberAttributeCountOptions { TargetUserId = targetUserId };
            uint count = lobbyDetails.GetMemberAttributeCount(ref countOpt);

            for (uint i = 0; i < count; i++)
            {
                var byIdx = new LobbyDetailsCopyMemberAttributeByIndexOptions
                {
                    TargetUserId = targetUserId,
                    AttrIndex = i,
                };
                if (lobbyDetails.CopyMemberAttributeByIndex(ref byIdx, out var attribute) == Result.Success)
                    list.Add(attribute);
            }

            return list;
        }

        public static List<Attribute?> GetAttributes(LobbyDetails lobbyDetails)
        {
            var list = new List<Attribute?>();
            if (lobbyDetails == null) return list;

            var countOpt = new LobbyDetailsGetAttributeCountOptions();
            uint count = lobbyDetails.GetAttributeCount(ref countOpt);

            for (uint i = 0; i < count; i++)
            {
                var byIdx = new LobbyDetailsCopyAttributeByIndexOptions { AttrIndex = i };
                if (lobbyDetails.CopyAttributeByIndex(ref byIdx, out var attribute) == Result.Success)
                    list.Add(attribute);
            }

            return list;
        }
    }
}
