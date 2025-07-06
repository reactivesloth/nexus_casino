using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CC
{
    public class Apparel_Menu : MonoBehaviour, ICustomizerUI
    {
        public GameObject ButtonPrefab;
        public GameObject Container;

        public TextMeshProUGUI OptionText;

        private CharacterCustomization customizer;

        private int navIndex = 0;
        private int optionsCount = 0;

        public bool useIcons = true;
        public Sprite defaultIcon;

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util parentUI)
        {
            customizer = customizerScript;
            RefreshUIElement();
        }

        public void createApparelButtons(int slot)
        {
            navIndex = slot;
            if (OptionText != null) OptionText.text = customizer.ApparelTables[slot].Label;

            foreach (Transform child in Container.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (var item in customizer.ApparelTables[slot].Items)
            {
                for (int j = 0; j < item.Materials.Count; j++)
                {
                    createButton(item.Name, slot, j, item.Materials[j].Icon);
                }

                if (item.Materials.Count == 0)
                {
                    createButton(item.Name, slot, 0, null);
                }
            }
        }

        private void createButton(string text, int slot, int material, Sprite sprite)
        {
            string name = text;
            int matIndex = material;
            int apparelSlot = slot;

            GameObject Button = Instantiate(ButtonPrefab, Container.transform).gameObject;
            Button.GetComponentInChildren<Button>().onClick.AddListener(() => { customizer.setApparelByName(name, apparelSlot, matIndex); });

            if (useIcons) Button.GetComponentInChildren<Image>().sprite = sprite == null ? defaultIcon : sprite;
            else Button.GetComponentInChildren<TextMeshProUGUI>().text = text;
        }

        public void navLeft()
        {
            createApparelButtons(navIndex == 0 ? optionsCount - 1 : navIndex - 1);
        }

        public void navRight()
        {
            createApparelButtons(navIndex == optionsCount - 1 ? 0 : navIndex + 1);
        }

        public void RefreshUIElement()
        {
            optionsCount = customizer.ApparelTables.Count;
            createApparelButtons(0);
        }
    }
}