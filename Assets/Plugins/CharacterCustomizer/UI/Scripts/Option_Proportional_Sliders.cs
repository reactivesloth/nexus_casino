using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CC
{
    public class Option_Proportional_Sliders : MonoBehaviour, ICustomizerUI
    {
        private CharacterCustomization _customizer;
        private CC_UI_Util _parentUI;

        public List<CC_Property> Properties = new List<CC_Property>();

        public bool RemoveText = true;
        public bool Vertical = true;

        public GameObject SliderObject;
        public Transform SliderContainer;

        private readonly List<Slider> _sliders = new List<Slider>();
        private readonly List<Option_Slider> _sliderScripts = new List<Option_Slider>();
        private float _sliderSum;

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util ParentUI)
        {
            _customizer = customizerScript;
            _parentUI = ParentUI;

            // Cleanup previous
            for (int i = 0; i < _sliderScripts.Count; i++)
            {
                if (_sliderScripts[i] != null)
                    Destroy(_sliderScripts[i].gameObject);
            }
            _sliderScripts.Clear();
            _sliders.Clear();

            if (SliderObject == null || SliderContainer == null || Properties == null) return;

            for (int i = 0; i < Properties.Count; i++)
            {
                var sliderObj = Instantiate(SliderObject, SliderContainer);
                if (sliderObj == null) continue;

                var sliderScript = sliderObj.AddComponent<Option_Slider>();
                if (RemoveText)
                {
                    var txt = sliderObj.GetComponentInChildren<TMP_Text>(true);
                    if (txt != null) txt.gameObject.SetActive(false);
                }

                _sliderScripts.Add(sliderScript);
                sliderScript.Property = Properties[i];
                sliderScript.CustomizationType = Option_Slider.Type.Blendshape;
                sliderScript.InitializeUIElement(customizerScript, ParentUI);

                var slider = sliderScript.GetComponentInChildren<Slider>(true);
                if (slider != null)
                {
                    if (Vertical) slider.SetDirection(Slider.Direction.BottomToTop, true);
                    _sliders.Add(slider);
                    slider.onValueChanged.AddListener(_ => checkExcess(slider));
                }
            }

            var parentRT = transform.parent as RectTransform;
            if (parentRT != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);
            }
        }

        public void RefreshUIElement()
        {
            for (int i = 0; i < _sliderScripts.Count; i++)
            {
                _sliderScripts[i]?.RefreshUIElement();
            }
        }

        public void checkExcess(Slider mainSlider)
        {
            if (mainSlider == null) return;

            _sliderSum = 0f;
            for (int i = 0; i < _sliders.Count; i++)
            {
                var s = _sliders[i];
                if (s != null) _sliderSum += s.value;
            }

            if (_sliderSum > 1f)
            {
                float sumWithoutMain = _sliderSum - mainSlider.value;
                float excess = _sliderSum - 1f;

                if (sumWithoutMain <= 0f) return;

                for (int i = 0; i < _sliders.Count; i++)
                {
                    if (_sliders[i] == null || _sliders[i] == mainSlider) continue;
                    distributeExcess(sumWithoutMain, excess, i);
                }
            }
        }

        public void distributeExcess(float sum, float excess, int index)
        {
            var s = _sliders[index];
            var sc = _sliderScripts[index];
            if (s == null || sc == null || sum <= 0f) return;

            float newVal = s.value - (s.value / sum) * excess;
            s.SetValueWithoutNotify(newVal);
            sc.setProperty(newVal);
        }

        private void OnDestroy()
        {
            // best-effort cleanup
            for (int i = 0; i < _sliders.Count; i++)
            {
                var s = _sliders[i];
                if (s != null) s.onValueChanged.RemoveAllListeners();
            }
        }
    }
}
