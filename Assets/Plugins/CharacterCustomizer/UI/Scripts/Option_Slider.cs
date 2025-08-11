using UnityEngine;
using UnityEngine.UI;

namespace CC
{
    public class Option_Slider : MonoBehaviour, ICustomizerUI
    {
        public enum Type { Blendshape, Scalar, ModifyType }

        public Type CustomizationType;
        public CC_Property Property;

        public Vector2 Range = new Vector2(0, 1);
        public float DefaultValue = 0f;
        public string DisplayOption = "Option";
        public bool UsesDragCustomization;
        public float dragMultiplier = 1f;

        private Slider _slider;
        private CharacterCustomization _customizer;

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += OnValidateCallback;
        }

        private void OnValidateCallback()
        {
            if (this == null || Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= OnValidateCallback;
                return;
            }

            _slider = GetComponentInChildren<Slider>(true);
            if (_slider != null)
            {
                _slider.minValue = Range.x;
                _slider.maxValue = Range.y;
                _slider.SetValueWithoutNotify(DefaultValue);
            }

            var txt = GetComponentInChildren<TMPro.TMP_Text>(true);
            if (txt != null) txt.text = DisplayOption;
            gameObject.name = "Slider_" + DisplayOption;
        }
#endif

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util ParentUI)
        {
            _slider = GetComponentInChildren<Slider>(true);
            _customizer = customizerScript;

            if (_slider != null)
            {
                _slider.minValue = Range.x;
                _slider.maxValue = Range.y;
                _slider.onValueChanged.AddListener(setProperty);
            }

            if (UsesDragCustomization && CC_UI_Manager.instance != null)
                CC_UI_Manager.instance.onDrag += onBodyCustomization;

            RefreshUIElement();
        }

        private void onBodyCustomization(string partX, string partY, float deltaX, float deltaY, bool first, bool last)
        {
            if (_slider == null) return;

            if (!string.IsNullOrEmpty(partX) && partX == Property.propertyName)
            {
                _slider.value += deltaX * dragMultiplier;
            }
            if (!string.IsNullOrEmpty(partY) && partY == Property.propertyName)
            {
                _slider.value += deltaY * dragMultiplier;
            }
        }

        public void RefreshUIElement()
        {
            if (_slider == null || _customizer == null) return;

            switch (CustomizationType)
            {
                case Type.Blendshape:
                case Type.ModifyType:
                {
                    if (_customizer.findProperty(_customizer.StoredCharacterData.Blendshapes, Property, out Property, out int _))
                        _slider.SetValueWithoutNotify(Property.floatValue);
                    else
                        _slider.SetValueWithoutNotify(DefaultValue);
                    break;
                }
                case Type.Scalar:
                {
                    if (_customizer.findProperty(_customizer.StoredCharacterData.FloatProperties, Property, out Property, out int _))
                        _slider.SetValueWithoutNotify(Property.floatValue);
                    else
                        _slider.SetValueWithoutNotify(DefaultValue);
                    break;
                }
            }
        }

        public void setProperty(float value)
        {
            if (_customizer == null) return;

            Property.floatValue = value;

            switch (CustomizationType)
            {
                case Type.Blendshape:
                case Type.ModifyType:
                    _customizer.setBlendshapeByName(Property.propertyName, value);
                    break;
                case Type.Scalar:
                    _customizer.setFloatProperty(Property, true);
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_slider != null) _slider.onValueChanged.RemoveListener(setProperty);
            if (UsesDragCustomization && CC_UI_Manager.instance != null)
                CC_UI_Manager.instance.onDrag -= onBodyCustomization;
        }
    }
}
