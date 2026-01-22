using PurrNet;

namespace Code.Network.Player
{
    public class Player : PlayerIdentity<Player>
    {
        public static Player GetLocalPlayer() => TryGetLocal(out var player) ? player : null;
    }
}