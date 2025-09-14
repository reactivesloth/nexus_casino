using UnityEngine;

namespace CC
{
    public class CharacterNonCustomization : MonoBehaviour
    {
        public GameObject UI;
        private GameObject UI_Instance;

        private void Start()
        {
            if (UI_Instance == null && UI != null && CC_UI_Manager.instance != null)
            {
                UI_Instance = Instantiate(UI, CC_UI_Manager.instance.transform);
                UI_Instance.SetActive(true);
            }
        }

        private void OnEnable()
        {
            if (UI_Instance != null) UI_Instance.SetActive(true);
        }

        private void OnDisable()
        {
            if (UI_Instance != null) UI_Instance.SetActive(false);
        }
    }
}