using UnityEngine;

namespace CC
{
    public class CC_UI_Util : MonoBehaviour
    {
        private CharacterCustomization _customizer;

        public void Initialize(CharacterCustomization customizerScript)
        {
            _customizer = customizerScript;

            var interfaces = gameObject.GetComponentsInChildren<ICustomizerUI>(true);
            if (interfaces == null) return;

            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i]?.InitializeUIElement(customizerScript, this);
            }
        }

        public void refreshUI()
        {
            var interfaces = gameObject.GetComponentsInChildren<ICustomizerUI>(true);
            if (interfaces == null) return;

            for (int i = 0; i < interfaces.Length; i++)
            {
                interfaces[i]?.RefreshUIElement();
            }
        }

        public void characterNext()
        {
            saveToJSON();
            if (CC_UI_Manager.instance != null) CC_UI_Manager.instance.characterNext();
        }

        public void characterPrev()
        {
            saveToJSON();
            if (CC_UI_Manager.instance != null) CC_UI_Manager.instance.characterPrev();
        }

        public void saveToPreset(string name)
        {
            _customizer?.SaveToPreset(name);
        }

        public void saveToJSON()
        {
            _customizer?.SaveToJSON();
        }

        public void loadCharacter()
        {
            _customizer?.LoadFromJSON();
            refreshUI();
        }

        public void setCharacterName(string newName)
        {
            _customizer?.setCharacterName(newName);
        }

        public void setCharacterPreset(string preset)
        {
            // reserved (no-op)
        }

        public void randomizeCharacter()
        {
            _customizer?.randomizeAll();
        }

        public void randomizeOutfit()
        {
            _customizer?.setRandomOutfit();
        }

        public void randomizeCharacterAndOutfit()
        {
            _customizer?.randomizeCharacterAndOutfit();
        }
    }
}
