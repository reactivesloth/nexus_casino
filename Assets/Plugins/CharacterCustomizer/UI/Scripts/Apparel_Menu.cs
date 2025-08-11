using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CC
{
    public class Apparel_Menu : MonoBehaviour, ICustomizerUI
    {
        [Header("Prefabs & UI")]
        public GameObject ButtonPrefab;
        public GameObject Container;
        public TextMeshProUGUI OptionText;

        [Header("Icons")]
        public bool useIcons = true;
        public Sprite defaultIcon;

        private CharacterCustomization _customizer;
        private Transform _containerTr;
        private int _navIndex;
        private int _optionsCount;

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util parentUI)
        {
            _customizer = customizerScript;
            _containerTr = Container != null ? Container.transform : null;
            RefreshUIElement();
        }

        public void RefreshUIElement()
        {
            if (_customizer == null || _customizer.ApparelTables == null || _customizer.ApparelTables.Count == 0)
            {
                _optionsCount = 0;
                ClearContainer();
                if (OptionText != null) OptionText.text = string.Empty;
                return;
            }

            _optionsCount = _customizer.ApparelTables.Count;
            createApparelButtons(0);
        }

        public void createApparelButtons(int slot)
        {
            if (_customizer == null || _customizer.ApparelTables == null || _customizer.ApparelTables.Count == 0)
            {
                ClearContainer();
                return;
            }

            if (slot < 0 || slot >= _customizer.ApparelTables.Count) slot = 0;
            _navIndex = slot;

            var table = _customizer.ApparelTables[slot];
            if (OptionText != null) OptionText.text = table != null ? table.Label : string.Empty;

            ClearContainer();

            if (_containerTr == null || ButtonPrefab == null || table == null || table.Items == null)
                return;

            var items = table.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                var materials = item.Materials;
                if (materials != null && materials.Count > 0)
                {
                    for (int j = 0; j < materials.Count; j++)
                    {
                        var icon = materials[j] != null ? materials[j].Icon : null;
                        createButton(item.Name, slot, j, icon);
                    }
                }
                else
                {
                    createButton(item.Name, slot, 0, null);
                }
            }
        }

        private void createButton(string text, int slot, int material, Sprite sprite)
        {
            if (ButtonPrefab == null || _containerTr == null || _customizer == null) return;

            var go = Instantiate(ButtonPrefab, _containerTr);
            if (go == null) return;

            // Components can be nested — use "InChildren"
            var uiButton = go.GetComponentInChildren<Button>(true);
            var uiImage = go.GetComponentInChildren<Image>(true);
            var uiText = go.GetComponentInChildren<TextMeshProUGUI>(true);

            if (uiButton != null)
            {
                string nameLocal = text;
                int matIndexLocal = material;
                int apparelSlotLocal = slot;
                uiButton.onClick.AddListener(() =>
                {
                    _customizer.setApparelByName(nameLocal, apparelSlotLocal, matIndexLocal);
                });
            }

            if (useIcons)
            {
                if (uiImage != null)
                    uiImage.sprite = sprite != null ? sprite : defaultIcon;
            }
            else
            {
                if (uiText != null) uiText.text = text ?? string.Empty;
            }
        }

        private void ClearContainer()
        {
            if (_containerTr == null) return;
            // Destroy children safely
            for (int i = _containerTr.childCount - 1; i >= 0; i--)
            {
                var child = _containerTr.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }
        }

        public void navLeft()
        {
            if (_optionsCount <= 0) return;
            int idx = (_navIndex == 0) ? _optionsCount - 1 : _navIndex - 1;
            createApparelButtons(idx);
        }

        public void navRight()
        {
            if (_optionsCount <= 0) return;
            int idx = (_navIndex == _optionsCount - 1) ? 0 : _navIndex + 1;
            createApparelButtons(idx);
        }
    }
}
