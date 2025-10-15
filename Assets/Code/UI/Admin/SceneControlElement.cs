using System.Collections.Generic;
using Code.Chat;
using Code.Scene.SceneObjectControl;
using TMPro;
using UnityEngine;

namespace Code.UI.Admin
{
    public class SceneControlElement: ControlElement
    {
        [SerializeField] private TMP_Dropdown statesDropdown;

        private IControlledSceneObject _sceneObject;
        private AdminPanelHandler _adminPanelHandler;
        
        private void Awake()
        {
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            statesDropdown.onValueChanged.AddListener(OnDropDownValueChanged);
        }

        private void LateUpdate()
        {
            if (statesDropdown.value != _sceneObject.CurrentStateIndex)
            {
                statesDropdown.SetValueWithoutNotify(_sceneObject.CurrentStateIndex);
            }
        }

        private void OnDisable()
        {
            statesDropdown.onValueChanged.RemoveListener(OnDropDownValueChanged);
        }

        public void Init(IControlledSceneObject sceneObject)
        {
            _sceneObject = sceneObject;
            
            SearchKey = _sceneObject.Key;
            titleDisplayText.text = _sceneObject.Key;
            statesDropdown.options = new List<TMP_Dropdown.OptionData>();
            
            _sceneObject.States.ForEach(s => statesDropdown.options.Add(new TMP_Dropdown.OptionData(s)));
            statesDropdown.SetValueWithoutNotify(_sceneObject.CurrentStateIndex);
        }

        private void OnDropDownValueChanged(int index)
        {
            var indexText = statesDropdown.options[index].text;
            _adminPanelHandler.SceneControl(_sceneObject.Key, indexText);
        }
    }
}