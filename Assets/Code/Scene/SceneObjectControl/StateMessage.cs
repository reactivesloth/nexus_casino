using FishNet.Broadcast;

namespace Code.Scene.SceneObjectControl
{
    public struct StateMessage: IBroadcast
    {
        public string ObjectName;
        public string Action;
    }
}