using FishNet.Broadcast;

namespace Code.Scene.SceneObjectControl
{
    public struct ActionMessage: IBroadcast
    {
        public string ObjectName;
        public string Action;
    }
}