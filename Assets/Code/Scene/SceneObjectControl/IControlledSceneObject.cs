namespace Code.Scene.SceneObjectControl
{
    public interface IControlledSceneObject
    {
        public string Name { get; }
        public void Action(string actionName);
    }
}