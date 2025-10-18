using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Code.Scene.SceneObjectControl
{
    public class ControlledSceneObject : MonoBehaviour, IControlledSceneObject
    {
        [SerializeField] private string objectName;
        [SerializeField] private List<StateInfo> states;

        private int _currentStateIndex = 0;

        public string Key => objectName;
        public List<string> States => states.Select(s => s.stateName).ToList();
        public int CurrentStateIndex => _currentStateIndex;

        private readonly Dictionary<string, StateInfo> _statesDictionary = new();

        private void Awake()
        {
            _statesDictionary.Clear();
            states.ForEach(s => _statesDictionary.Add(s.stateName, s));
        }

        public void SetState(string stateName)
        {
            if (!_statesDictionary.TryGetValue(stateName, out var stateInfo))
            {
                Debug.LogWarning($"[SceneControl] Object not contains state {stateName}");
                return;
            }
            
            _currentStateIndex = States.IndexOf(stateName);
            stateInfo.stateAction?.Invoke();
        }
    }

    [Serializable]
    public struct StateInfo
    {
        public string stateName;
        public UnityEvent stateAction;
    }
}