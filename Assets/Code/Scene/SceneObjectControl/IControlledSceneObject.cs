using System.Collections.Generic;

namespace Code.Scene.SceneObjectControl
{
    public interface IControlledSceneObject
    {
        public string Key { get; }
        public List<string> States { get; }
        public int CurrentStateIndex { get; }
        
        public void SetState(string stateName);
    }
}