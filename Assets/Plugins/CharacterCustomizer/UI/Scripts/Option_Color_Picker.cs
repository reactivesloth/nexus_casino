using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CC
{
    public class Option_Color_Picker : MonoBehaviour, ICustomizerUI
    {
        private CharacterCustomization _customizer;
        private CC_UI_Util _parentUI;

        public CC_Property Property;
        public bool useOpacity;
        public string DisplayOption = "Option";
        public GameObject hsvSliders; // prefab
        public Image pickerIcon;

        private Image[] _imgs;
        private GameObject _activeSliderObj;

        private float h, s, v, a = 1f;

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

            var txt = GetComponentInChildren<TMPro.TMP_Text>();
            if (txt != null) txt.text = DisplayOption;
            gameObject.name = "ColorPicker_" + DisplayOption;
        }
#endif

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util ParentUI)
        {
            _customizer = customizerScript;
            _parentUI = ParentUI;
            RefreshUIElement();
        }

        public void RefreshUIElement()
        {
            if (_customizer == null) return;

            if (_customizer.findProperty(_customizer.StoredCharacterData.ColorProperties, Property, out Property, out int _))
            {
                if (pickerIcon != null)
                {
                    var c = Property.colorValue;
                    pickerIcon.color = new Color(c.r, c.g, c.b, 1f);
                }
            }
        }

        public void toggleSliders()
        {
            if (_parentUI == null || hsvSliders == null) return;

            // Close previous
            if (_activeSliderObj != null)
            {
                Destroy(_activeSliderObj);
                _activeSliderObj = null;
            }

            Color.RGBToHSV(Property.colorValue, out h, out s, out v);
            a = Property.colorValue.a;

            var sliderObj = Instantiate(hsvSliders, _parentUI.transform);
            _activeSliderObj = sliderObj;
            if (sliderObj == null) return;

            var eventTrigger = sliderObj.GetComponentInChildren<EventTrigger>();
            if (eventTrigger != null)
            {
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown, callback = new EventTrigger.TriggerEvent() };
                entry.callback.AddListener(_ => { if (_activeSliderObj != null) { Destroy(_activeSliderObj); _activeSliderObj = null; } });
                eventTrigger.triggers.Add(entry);
            }

            // position near this
            if (sliderObj.transform.childCount > 1)
            {
                var sliderContainer = sliderObj.transform.GetChild(1);
                sliderContainer.position = transform.position;
            }

            var sliders = sliderObj.GetComponentsInChildren<Slider>(true);
            _imgs = sliderObj.GetComponentsInChildren<Image>(true);

            if (sliders != null && sliders.Length >= 3)
            {
                sliders[0].SetValueWithoutNotify(h);
                sliders[1].SetValueWithoutNotify(s);
                sliders[2].SetValueWithoutNotify(v);

                sliders[0].onValueChanged.AddListener(f => { h = f; setColor(); });
                sliders[1].onValueChanged.AddListener(f => { s = f; setColor(); });
                sliders[2].onValueChanged.AddListener(f => { v = f; setColor(); });

                if (useOpacity && sliders.Length >= 4)
                {
                    sliders[3].SetValueWithoutNotify(a);
                    sliders[3].onValueChanged.AddListener(f => { a = f; setColor(); });
                }
                else if (!useOpacity && sliders.Length >= 4)
                {
                    var opacityParent = sliders[3].transform.parent != null ? sliders[3].transform.parent.gameObject : null;
                    if (opacityParent != null) Destroy(opacityParent);
                }
            }

            if (_imgs != null)
            {
                var hueColor = Color.HSVToRGB(h, 1f, 1f);
                for (int i = 0; i < _imgs.Length; i++)
                {
                    if (_imgs[i] != null && !_imgs[i].raycastTarget) _imgs[i].color = hueColor;
                }
            }

            var rt = sliderObj.transform as RectTransform;
            if (rt != null && rt.parent is RectTransform parentRT)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);
            }
        }

        public void setColor()
        {
            Property.colorValue = Color.HSVToRGB(h, s, v);
            Property.colorValue.a = a;

            if (_customizer != null)
                _customizer.setColorProperty(Property, true);

            if (pickerIcon != null)
            {
                var c = Property.colorValue;
                pickerIcon.color = new Color(c.r, c.g, c.b, 1f);
            }

            if (_imgs != null)
            {
                var hueColor = Color.HSVToRGB(h, 1f, 1f);
                for (int i = 0; i < _imgs.Length; i++)
                {
                    if (_imgs[i] != null && !_imgs[i].raycastTarget) _imgs[i].color = hueColor;
                }
            }
        }

        private void OnDisable()
        {
            if (_activeSliderObj != null)
            {
                Destroy(_activeSliderObj);
                _activeSliderObj = null;
            }
        }
    }
}
