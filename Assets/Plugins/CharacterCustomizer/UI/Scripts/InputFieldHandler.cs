using System.Reflection;
using UnityEngine;

namespace CC
{
    public class InputFieldHandler : MonoBehaviour, ICustomizerUI
    {
        private TMPro.TMP_InputField _inputField;
        private CC_UI_Util _ui;

        private void Awake()
        {
            _inputField = GetComponent<TMPro.TMP_InputField>();
        }

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util parentUI)
        {
            _ui = parentUI;
        }

        public void RefreshUIElement() { }

        public void InvokeMethod(string methodName)
        {
            if (_ui == null || _inputField == null || string.IsNullOrEmpty(methodName)) return;

            MethodInfo method = typeof(CC_UI_Util).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return;

            var prms = method.GetParameters();
            if (prms.Length == 1 && prms[0].ParameterType == typeof(string))
            {
                method.Invoke(_ui, new object[] { _inputField.text ?? string.Empty });
            }
        }
    }
}