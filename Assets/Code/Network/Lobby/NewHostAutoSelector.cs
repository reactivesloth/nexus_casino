using System.Collections.Generic;
using System.Linq;
using Code.API.Models;
using Code.Network.Lobby.Data;

namespace Code.Network.Lobby
{
    public static class NewHostAutoSelector
    {
        private const long GoodPing = 300;

        public static string GetNewHostIdAuto()
        {
            var members = LobbyVariables.Instance.currentLobby.lobbyMembers;

            var adminMembers = members
                .Where(m => m.Attributes.TryGetValue("ROLE", out var role) && MeSchema.CheckAdmin(role)).ToList();

            if (adminMembers.Count > 0)
                return SelectWithBestPing(adminMembers);

            var goodPingMembers =
                members.Where(m => m.Attributes.TryGetValue("PING", out var ping) && long.Parse(ping) <= GoodPing)
                    .ToList();
            
            if (goodPingMembers.Count == 0)
                return SelectWithBestPing(members);
            
            return SelectWithBestHardware(goodPingMembers);
        }

        private static string SelectWithBestPing(List<LobbyData.LobbyMember> members)
        {
            var membersWithPing = members
                .Where(m => m.Attributes.TryGetValue("PING", out _)
                ).OrderBy(m => long.Parse(m.Attributes["PING"])).ToList();
            return membersWithPing.Count > 0 ? membersWithPing.First().productUserId : members.First().productUserId;
        }

        private static string SelectWithBestHardware(List<LobbyData.LobbyMember> members)
        {
            var membersWithPing = members
                .Where(m => m.Attributes.TryGetValue("HARDWARE_SCORE", out _)
                ).OrderBy(m => long.Parse(m.Attributes["HARDWARE_SCORE"])).ToList();
            return membersWithPing.Count > 0 ? membersWithPing.First().productUserId : members.First().productUserId;
        }
    }
}