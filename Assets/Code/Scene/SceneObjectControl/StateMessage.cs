
using PurrNet.Packing;

namespace Code.Scene.SceneObjectControl
{
    public struct StateMessage : IPackedAuto
    {
        public string ObjectName;
        public string Action;
    }
}