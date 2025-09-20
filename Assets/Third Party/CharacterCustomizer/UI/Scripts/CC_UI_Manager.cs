using System;
using System.Collections.Generic;
using Code.API;
using Code.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CC
{
    [DefaultExecutionOrder(200)]
    public class CC_UI_Manager : MonoBehaviour
    {
        public static CC_UI_Manager instance;

        public delegate void OnHover(string partHovered);
        public event OnHover onHover;

        public delegate void OnDrag(string partX, string partY, float deltaX, float deltaY, bool first, bool last);
        public event OnDrag onDrag;

        private bool Dragging;
        private string hoveredPart = "";
        private string partX, partY = "";
        private float multX, multY = 1f;
        public float mouseDeltaScale = 0.01f;
        private Vector3 mousePos;

        private Canvas canvas;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [Tooltip("The parent object of your customizable characters")]
        public GameObject CharacterParent;

        private int currentCharacter;

        public List<AudioClip> UISounds = new List<AudioClip>();

        public void Start()
        {
            string playerModelType = PlayerPrefs.GetString("PlayerModelType", "Male");
            int index = PlayerModelTypeToIndex(playerModelType);
            SetActiveCharacter(index);
        }

        private void Update()
        {
            bool first = !Dragging && Input.GetMouseButton(0);
            bool last = Dragging && !Input.GetMouseButton(0);

            //Set shape on first drag
            if (first)
            {
                partX = ""; partY = "";
                multX = hoveredPart.Contains("_r") ? -1 : 1; multY = -1f;

                if (hoveredPart.Contains("spine_05")) { partX = "BodyCustomization_ShoulderWidth"; partY = "BodyCustomization_TorsoHeight"; }
                else if (hoveredPart.Contains("spine")) { partX = "BodyCustomization_WaistSize"; partY = ""; }
                else if (hoveredPart.Contains("pelvis")) { partX = "BodyCustomization_HipWidth"; partY = ""; }
                else if (hoveredPart.Contains("lowerarm")) { partX = "BodyCustomization_LowerArmScale"; partY = ""; }
                else if (hoveredPart.Contains("upperarm")) { partX = "BodyCustomization_UpperArmScale"; partY = ""; }
                else if (hoveredPart.Contains("thigh")) { partX = "BodyCustomization_ThighScale"; partY = ""; }
                else if (hoveredPart.Contains("calf")) { partX = "BodyCustomization_CalfScale"; partY = ""; }
                else if (hoveredPart.Contains("head")) { partX = "BodyCustomization_HeadSize"; partY = "BodyCustomization_NeckLength"; }
                else if (hoveredPart.Contains("neck")) { partX = "BodyCustomization_NeckScale"; partY = "BodyCustomization_NeckLength"; }
                else if (hoveredPart.Contains("collider_nose")) { partX = "mod_nose_size"; partY = "mod_nose_height"; multY = 1; }
                else if (hoveredPart.Contains("collider_mouth")) { partX = "mod_mouth_size"; partY = "mod_mouth_height"; multY = 1; }
                else if (hoveredPart.Contains("collider_cheekbones")) { partX = "mod_cheekbone_size"; partY = ""; multX *= -1; }
                else if (hoveredPart.Contains("collider_cheeks")) { partX = "mod_cheeks_size"; partY = ""; multX *= -1; }
                else if (hoveredPart.Contains("collider_jaw")) { partX = "mod_jaw_width"; partY = "mod_jaw_height"; multX *= -1; }
                else if (hoveredPart.Contains("collider_chin")) { partX = ""; partY = "mod_chin_size"; }
                else if (hoveredPart.Contains("collider_eye")) { partX = "mod_eyes_narrow"; partY = "mod_eyes_height"; multY = 1; }
                else if (hoveredPart.Contains("collider_brow")) { partX = ""; partY = "mod_brow_height"; }
            }

            Dragging = Input.GetMouseButton(0);

            if (Dragging && getCanvas() != null)
            {
                Vector3 mouseDelta = (Input.mousePosition - mousePos) * mouseDeltaScale / canvas.scaleFactor;
                onDrag?.Invoke(partX, partY, mouseDelta.x * multX, mouseDelta.y * multY, first, last);
            }
            mousePos = Input.mousePosition;
        }

        private Canvas getCanvas()
        {
            if (canvas != null) return canvas;
            else
            {
                canvas = GetComponentInChildren<Canvas>();
                return canvas;
            }
        }

        private void LateUpdate()
        {
            if (Dragging) return;
            onHover?.Invoke(hoveredPart);

            Physics.SyncTransforms();

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit) && !EventSystem.current.IsPointerOverGameObject())
            {
                hoveredPart = hit.collider.name;
            }
            else hoveredPart = "";
        }

        public void playUIAudio(int Index)
        {
            var audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource && UISounds.Count > Index) audioSource.clip = UISounds[Index]; audioSource.Play();
        }

        public void SetActiveCharacter(int i)
        {
            if (CharacterParent == null) return;

            int childCount = CharacterParent.transform.childCount;
            if (childCount == 0) return;
            
            if (i < 0 || i >= childCount) i = 0;
            int prevIndex = currentCharacter;
            currentCharacter = i;
            
            SavePlayerModelType(i);

            for (int j = 0; j < childCount; j++)
            {
                Transform tr = CharacterParent.transform.GetChild(j);
                if (tr == null) continue;

                GameObject character = tr.gameObject;
                if (character == null) continue;

                if (j == i)
                {
                    if (character.GetComponentInChildren<CharacterRoleFilter>() != null)
                    {
                        if (ClientDataStorage.UserData.IsAdminRole !=
                            character.GetComponentInChildren<CharacterRoleFilter>().IsAdminRole)
                        {
                            character.SetActive(false);
                            if (prevIndex > currentCharacter || currentCharacter == 0)
                                characterNext();
                            if (prevIndex < currentCharacter)
                                characterPrev();
                        }
                        else
                        {
                            character.SetActive(true);
                        }
                    }
                    else
                    {
                        character.SetActive(true);
                    }
                }
                else
                {
                    if (character.activeSelf)
                    {
                        var script = character.GetComponentInChildren<CharacterCustomization>();
                        if (script != null)
                        {
                            //script.LoadFromPreset(script.CharacterName);
                            script.LoadFromJSON();
                        }
                        character.SetActive(false);
                    }
                }
            }
        }

        public void characterNext()
        {
            if (CharacterParent == null) return;
            int count = CharacterParent.transform.childCount;
            if (count == 0) return;

            int next = (currentCharacter == count - 1) ? 0 : currentCharacter + 1;
            SetActiveCharacter(next);
        }

        public void characterPrev()
        {
            if (CharacterParent == null) return;
            int count = CharacterParent.transform.childCount;
            if (count == 0) return;

            int prev = (currentCharacter == 0) ? count - 1 : currentCharacter - 1;
            SetActiveCharacter(prev);
        }

        private static int PlayerModelTypeToIndex(string type)
        {
            // map known types
            switch (type)
            {
                case "Female": return 0;
                case "Male": return 1;
                case "PlayerM1": return 2;
                case "PlayerM2": return 3;
                case "PlayerM3": return 4;
                case "ChostisF1": return 5;
                default: return 1;
            }
        }

        private void SavePlayerModelType(int index)
        {
            string val = "Male";
            switch (index)
            {
                case 0: val = "Female"; break;
                case 1: val = "Male"; break;
                case 2: val = "PlayerM1"; break;
                case 3: val = "PlayerM2"; break;
                case 4: val = "PlayerM3"; break;
                case 5: val = "ChostisF1"; break;
            }
            PlayerPrefs.SetString("PlayerModelType", val);
        }
    }
}
