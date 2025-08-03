using System;
using UnityEngine;
using UnityEngine.Events;

namespace Code.Chat
{
    [Serializable]
    public class CommandData
    {
        public string commandValue;
        public bool requireMessageValue = false;
        public UnityEvent<string> unityEvent;

        [TextArea] public string description;
    }
}