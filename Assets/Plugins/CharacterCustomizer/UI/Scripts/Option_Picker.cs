using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace CC
{
    public class Option_Picker : MonoBehaviour, ICustomizerUI
    {
        private CharacterCustomization _customizer;

        public enum Type { Blendshape, Texture, Hair, Color, Stencil }
        public Type CustomizationType;

        public CC_Property Property;
        public List<CC_Property> Options = new List<CC_Property>();
        public int Slot = 0;

        public TextMeshProUGUI PropertyText;
        public TextMeshProUGUI OptionText;

        public string DisplayOption;

        private int _navIndex;
        private int _optionsCount;

        public bool valueFromIndex = true;
        public bool valueFromString = false;
        public bool blendshapeUseOptionValue;
        public List<CC_Stencil> stencilOptions = new List<CC_Stencil>();

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util ParentUI)
        {
            _customizer = customizerScript;
            RefreshUIElement();

            // propagate base settings
            for (int i = 0; i < stencilOptions.Count; i++)
            {
                var item = stencilOptions[i];
                if (item == null) continue;
                item.basePropertyName = Property.propertyName;
                item.materialIndex = Property.materialIndex;
                item.meshTag = Property.meshTag;
            }

            if (!blendshapeUseOptionValue && CustomizationType == Type.Blendshape) return;

            for (int i = 0; i < Options.Count; i++)
            {
                var item = Options[i];
                if (item == null) continue;
                item.propertyName = Property.propertyName;
                item.materialIndex = Property.materialIndex;
                item.meshTag = Property.meshTag;
            }
        }

        public void RefreshUIElement()
        {
            if (_customizer == null) return;

            switch (CustomizationType)
            {
                case Type.Blendshape:
                {
                    _optionsCount = Options != null ? Options.Count : 0;
                    updateOptionText();

                    if (Options != null)
                    {
                        for (int i = 0; i < Options.Count; i++)
                        {
                            CC_Property prop;
                            if (_customizer.findProperty(_customizer.StoredCharacterData.Blendshapes, Options[i], out prop, out int _))
                            {
                                if (prop.floatValue != 0f)
                                {
                                    _navIndex = i;
                                    updateOptionText();
                                    break;
                                }
                            }
                        }
                    }
                    break;
                }
                case Type.Texture:
                {
                    _optionsCount = Options != null ? Options.Count : 0;
                    if (_customizer.findProperty(_customizer.StoredCharacterData.TextureProperties, Property, out Property, out int _))
                    {
                        _navIndex = 0;
                        if (Options != null)
                        {
                            string saved = Property.stringValue ?? string.Empty;
                            for (int i = 0; i < Options.Count; i++)
                            {
                                if (Options[i] != null && Options[i].stringValue == saved) { _navIndex = i; break; }
                            }
                        }
                    }
                    else _navIndex = 0;
                    updateOptionText();
                    break;
                }
                case Type.Stencil:
                {
                    _optionsCount = stencilOptions != null ? stencilOptions.Count : 0;
                    if (_customizer.findProperty(_customizer.StoredCharacterData.TextureProperties, Property, out Property, out int _))
                    {
                        _navIndex = 0;
                        if (stencilOptions != null)
                        {
                            string saved = Property.stringValue ?? string.Empty;
                            for (int i = 0; i < stencilOptions.Count; i++)
                            {
                                var st = stencilOptions[i];
                                if (st != null && st.texture != null && st.texture.name == saved) { _navIndex = i; break; }
                            }
                        }
                    }
                    else _navIndex = 0;
                    updateOptionText();
                    break;
                }
                case Type.Color:
                {
                    _optionsCount = Options != null ? Options.Count : 0;
                    if (_customizer.findProperty(_customizer.StoredCharacterData.ColorProperties, Property, out Property, out int _))
                    {
                        _navIndex = 0;
                        if (Options != null)
                        {
                            for (int i = 0; i < Options.Count; i++)
                            {
                                if (Options[i] != null && colorMatch(Options[i].colorValue, Property.colorValue))
                                { _navIndex = i; break; }
                            }
                        }
                    }
                    else _navIndex = 0;
                    updateOptionText();
                    break;
                }
                case Type.Hair:
                {
                    if (_customizer.HairTables == null || _customizer.HairTables.Count <= Slot)
                    {
                        Destroy(gameObject);
                        return;
                    }

                    var table = _customizer.HairTables[Slot];
                    _optionsCount = table != null && table.Hairstyles != null ? table.Hairstyles.Count : 0;
                    _navIndex = 0;

                    if (table != null && table.Hairstyles != null)
                    {
                        string saved = (_customizer.StoredCharacterData != null && _customizer.StoredCharacterData.HairNames != null && _customizer.StoredCharacterData.HairNames.Count > Slot)
                                       ? _customizer.StoredCharacterData.HairNames[Slot] : null;
                        for (int i = 0; i < table.Hairstyles.Count; i++)
                        {
                            var hs = table.Hairstyles[i];
                            if (hs.Name == saved) { _navIndex = i; break; }
                        }
                    }
                    updateOptionText();
                    break;
                }
            }
        }

        private static bool colorMatch(Color a, Color b, float tolerance = 0.1f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance;
        }

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

            if (PropertyText != null) PropertyText.text = DisplayOption;
            if (!string.IsNullOrEmpty(DisplayOption)) gameObject.name = "Picker_" + DisplayOption;
        }
#endif

        public void updateOptionText()
        {
            if (OptionText == null) return;

            if (_optionsCount <= 0)
            {
                OptionText.gameObject.SetActive(true);
                OptionText.SetText("0/0");
                return;
            }

            if (valueFromIndex)
            {
                OptionText.gameObject.SetActive(true);
                OptionText.SetText((_navIndex + 1) + "/" + _optionsCount);
            }
            else if (valueFromString && Options != null && _navIndex >= 0 && _navIndex < Options.Count && Options[_navIndex] != null)
            {
                OptionText.gameObject.SetActive(true);
                OptionText.SetText(Options[_navIndex].stringValue);
            }
        }

        public void setOption(int i)
        {
            if (_optionsCount <= 0) return;
            if (i < 0) i = 0;
            if (i >= _optionsCount) i = _optionsCount - 1;

            _navIndex = i;
            updateOptionText();

            if (_customizer == null) return;

            switch (CustomizationType)
            {
                case Type.Blendshape:
                {
                    if (Options == null || _navIndex < 0 || _navIndex >= Options.Count) return;

                    if (blendshapeUseOptionValue)
                    {
                        _customizer.setBlendshapeByName(Property.propertyName, Options[_navIndex].floatValue);
                        break;
                    }
                    // Turn off others, enable this
                    for (int k = 0; k < Options.Count; k++)
                    {
                        var p = Options[k];
                        if (p != null) _customizer.setBlendshapeByName(p.propertyName, k == _navIndex ? 1f : 0f);
                    }
                    break;
                }
                case Type.Hair:
                {
                    _customizer.setHair(_navIndex, Slot);
                    break;
                }
                case Type.Texture:
                {
                    if (Options == null || _navIndex < 0 || _navIndex >= Options.Count) return;

                    var opt = Options[_navIndex];
                    if (opt == null) return;

                    string[] pNames = (opt.propertyName ?? string.Empty).Split(',');
                    string[] pValues = (opt.stringValue ?? string.Empty).Split(',');
                    string[] pTags = (opt.meshTag ?? string.Empty).Split(',');

                    int len = pNames.Length;
                    for (int j = 0; j < len; j++)
                    {
                        string value = (j < pValues.Length) ? pValues[j] : string.Empty;
                        string tag = (j < pTags.Length) ? pTags[j] : string.Empty;

                        var prop2 = new CC_Property
                        {
                            propertyName = pNames[j],
                            stringValue = value,
                            materialIndex = Property.materialIndex,
                            meshTag = tag
                        };
                        _customizer.setTextureProperty(prop2, true);
                    }
                    break;
                }
                case Type.Stencil:
                {
                    if (stencilOptions == null || _navIndex < 0 || _navIndex >= stencilOptions.Count) return;
                    var stencil = stencilOptions[_navIndex];
                    if (stencil == null) return;

                    var textureProp = new CC_Property { propertyName = stencil.basePropertyName, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    _customizer.setTextureProperty(textureProp, true, stencil.texture);

                    _customizer.setFloatProperty(new CC_Property { propertyName = stencil.basePropertyName + "_Offset_X", floatValue = stencil.offsetX, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag }, true);
                    _customizer.setFloatProperty(new CC_Property { propertyName = stencil.basePropertyName + "_Offset_Y", floatValue = stencil.offsetY, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag }, true);
                    _customizer.setFloatProperty(new CC_Property { propertyName = stencil.basePropertyName + "_Scale_X", floatValue = stencil.scaleX, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag }, true);
                    _customizer.setFloatProperty(new CC_Property { propertyName = stencil.basePropertyName + "_Scale_Y", floatValue = stencil.scaleY, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag }, true);
                    _customizer.setFloatProperty(new CC_Property { propertyName = stencil.basePropertyName + "_Rotation", floatValue = stencil.rotation, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag }, true);
                    _customizer.setFloatProperty(new CC_Property { propertyName = stencil.basePropertyName + "_Tintable", floatValue = stencil.tintable ? 1f : 0f, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag }, true);
                    break;
                }
                case Type.Color:
                {
                    if (Options == null || _navIndex < 0 || _navIndex >= Options.Count) return;
                    _customizer.setColorProperty(Options[_navIndex], true);
                    break;
                }
            }
        }

        public void navLeft()
        {
            if (_optionsCount <= 0) return;
            int idx = (_navIndex == 0) ? _optionsCount - 1 : _navIndex - 1;
            setOption(idx);
        }

        public void navRight()
        {
            if (_optionsCount <= 0) return;
            int idx = (_navIndex == _optionsCount - 1) ? 0 : _navIndex + 1;
            setOption(idx);
        }
    }
}
