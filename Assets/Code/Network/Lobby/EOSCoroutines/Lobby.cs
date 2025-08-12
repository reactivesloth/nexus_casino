using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;

namespace Code.Network.Lobby.EOSCoroutines
{
    /// <summary>
    /// Утилиты для чтения сведений о лобби через EOS.
    /// Без LINQ, с нулевыми гвардами и предвыделением списков.
    /// Поведение идентично исходному.
    /// </summary>
    public static class Lobby
    {
        public static Result GetLobbyDetails(out LobbyDetails lobbyDetailsHandle, Utf8String lobbyId, ProductUserId localUserId)
        {
            var lobbyInterface = EOS.GetPlatformInterface()?.GetLobbyInterface();
            if (lobbyInterface == null)
            {
                lobbyDetailsHandle = null;
                return Result.UnexpectedError;
            }

            var opt = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = lobbyId,
                LocalUserId = localUserId
            };
            return lobbyInterface.CopyLobbyDetailsHandle(ref opt, out lobbyDetailsHandle);
        }

        public static Result GetLobbyInfo(LobbyDetails lobbyDetail, out LobbyDetailsInfo? lobbyInfo)
        {
            if (lobbyDetail == null)
            {
                lobbyInfo = null;
                return Result.UnexpectedError;
            }

            var opt = new LobbyDetailsCopyInfoOptions();
            return lobbyDetail.CopyInfo(ref opt, out lobbyInfo);
        }

        public static Result GetAttribute(LobbyDetails lobbyDetail, string attrKey, out Attribute? lobbyAttribute)
        {
            if (lobbyDetail == null)
            {
                lobbyAttribute = null;
                return Result.UnexpectedError;
            }

            var opt = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = attrKey };
            return lobbyDetail.CopyAttributeByKey(ref opt, out lobbyAttribute);
        }

        public static Result GetMemberAttribute(LobbyDetails lobbyDetail, ProductUserId productUserId, string attrKey, out Attribute? lobbyAttribute)
        {
            if (lobbyDetail == null)
            {
                lobbyAttribute = null;
                return Result.UnexpectedError;
            }

            var opt = new LobbyDetailsCopyMemberAttributeByKeyOptions
            {
                AttrKey = attrKey,
                TargetUserId = productUserId
            };
            return lobbyDetail.CopyMemberAttributeByKey(ref opt, out lobbyAttribute);
        }

        public static List<ProductUserId> GetMembers(LobbyDetails lobbyDetails)
        {
            var result = new List<ProductUserId>();
            if (lobbyDetails == null) return result;

            var countOpt = new LobbyDetailsGetMemberCountOptions();
            uint count = lobbyDetails.GetMemberCount(ref countOpt);
            if (count == 0) return result;

            result = new List<ProductUserId>((int)count);
            for (uint i = 0; i < count; i++)
            {
                var byIdx = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                var member = lobbyDetails.GetMemberByIndex(ref byIdx);
                if (member != null)
                    result.Add(member);
            }
            return result;
        }

        public static List<Attribute?> GetMemberAttributes(LobbyDetails lobbyDetails, ProductUserId targetUserId)
        {
            var result = new List<Attribute?>();
            if (lobbyDetails == null || targetUserId == null) return result;

            var countOpt = new LobbyDetailsGetMemberAttributeCountOptions { TargetUserId = targetUserId };
            uint count = lobbyDetails.GetMemberAttributeCount(ref countOpt);
            if (count == 0) return result;

            result = new List<Attribute?>((int)count);
            for (uint i = 0; i < count; i++)
            {
                var byIdx = new LobbyDetailsCopyMemberAttributeByIndexOptions
                {
                    TargetUserId = targetUserId,
                    AttrIndex = i
                };
                if (lobbyDetails.CopyMemberAttributeByIndex(ref byIdx, out var attribute) == Result.Success)
                    result.Add(attribute);
            }
            return result;
        }

        public static List<Attribute?> GetAttributes(LobbyDetails lobbyDetails)
        {
            var result = new List<Attribute?>();
            if (lobbyDetails == null) return result;

            var countOpt = new LobbyDetailsGetAttributeCountOptions();
            uint count = lobbyDetails.GetAttributeCount(ref countOpt);
            if (count == 0) return result;

            result = new List<Attribute?>((int)count);
            for (uint i = 0; i < count; i++)
            {
                var byIdx = new LobbyDetailsCopyAttributeByIndexOptions { AttrIndex = i };
                if (lobbyDetails.CopyAttributeByIndex(ref byIdx, out var attribute) == Result.Success)
                    result.Add(attribute);
            }
            return result;
        }
    }
}
