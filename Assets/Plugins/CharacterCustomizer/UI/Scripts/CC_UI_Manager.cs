using System.Collections.Generic;
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

        [Tooltip("The parent object of your customizable characters")]
        public GameObject CharacterParent;

        public List<AudioClip> UISounds = new List<AudioClip>();
        public float mouseDeltaScale = 0.01f;

        private bool _dragging;
        private string _hoveredPart = "";
        private string _partX = "", _partY = "";
        private float _multX = 1f, _multY = 1f;
        private Vector3 _mousePos;
        private Canvas _canvas;
        private Camera _mainCam;
        private int _currentCharacter;

        private void Awake()
        {
            if (instance == null) instance = this;
            else { Destroy(gameObject); return; }
        }

        public void Start()
        {
            _mainCam = Camera.main;

            string playerModelType = PlayerPrefs.GetString("PlayerModelType", "Male");
            int index = PlayerModelTypeToIndex(playerModelType);
            SetActiveCharacter(index);
        }

        private void Update()
        {
            bool first = !_dragging && Input.GetMouseButton(0);
            bool last = _dragging && !Input.GetMouseButton(0);

            if (first)
            {
                _partX = ""; _partY = "";
                _multX = _hoveredPart != null && _hoveredPart.Contains("_r") ? -1f : 1f;
                _multY = -1f;

                if (!string.IsNullOrEmpty(_hoveredPart))
                {
                    if (_hoveredPart.Contains("spine_05")) { _partX = "BodyCustomization_ShoulderWidth"; _partY = "BodyCustomization_TorsoHeight"; }
                    else if (_hoveredPart.Contains("spine")) { _partX = "BodyCustomization_WaistSize"; _partY = ""; }
                    else if (_hoveredPart.Contains("pelvis")) { _partX = "BodyCustomization_HipWidth"; _partY = ""; }
                    else if (_hoveredPart.Contains("lowerarm")) { _partX = "BodyCustomization_LowerArmScale"; _partY = ""; }
                    else if (_hoveredPart.Contains("upperarm")) { _partX = "BodyCustomization_UpperArmScale"; _partY = ""; }
                    else if (_hoveredPart.Contains("thigh")) { _partX = "BodyCustomization_ThighScale"; _partY = ""; }
                    else if (_hoveredPart.Contains("calf")) { _partX = "BodyCustomization_CalfScale"; _partY = ""; }
                    else if (_hoveredPart.Contains("head")) { _partX = "BodyCustomization_HeadSize"; _partY = "BodyCustomization_NeckLength"; }
                    else if (_hoveredPart.Contains("neck")) { _partX = "BodyCustomization_NeckScale"; _partY = "BodyCustomization_NeckLength"; }
                    else if (_hoveredPart.Contains("collider_nose")) { _partX = "mod_nose_size"; _partY = "mod_nose_height"; _multY = 1f; }
                    else if (_hoveredPart.Contains("collider_mouth")) { _partX = "mod_mouth_size"; _partY = "mod_mouth_height"; _multY = 1f; }
                    else if (_hoveredPart.Contains("collider_cheekbones")) { _partX = "mod_cheekbone_size"; _partY = ""; _multX *= -1f; }
                    else if (_hoveredPart.Contains("collider_cheeks")) { _partX = "mod_cheeks_size"; _partY = ""; _multX *= -1f; }
                    else if (_hoveredPart.Contains("collider_jaw")) { _partX = "mod_jaw_width"; _partY = "mod_jaw_height"; _multX *= -1f; }
                    else if (_hoveredPart.Contains("collider_chin")) { _partX = ""; _partY = "mod_chin_size"; }
                    else if (_hoveredPart.Contains("collider_eye")) { _partX = "mod_eyes_narrow"; _partY = "mod_eyes_height"; _multY = 1f; }
                    else if (_hoveredPart.Contains("collider_brow")) { _partX = ""; _partY = "mod_brow_height"; }
                }
            }

            _dragging = Input.GetMouseButton(0);

            var canvas = GetCanvas();
            if (_dragging && canvas != null)
            {
                Vector3 mouseDelta = (Input.mousePosition - _mousePos) * mouseDeltaScale / canvas.scaleFactor;
                onDrag?.Invoke(_partX, _partY, mouseDelta.x * _multX, mouseDelta.y * _multY, first, last);
            }
            _mousePos = Input.mousePosition;
        }

        private Canvas GetCanvas()
        {
            if (_canvas != null) return _canvas;
            _canvas = GetComponentInChildren<Canvas>();
            return _canvas;
        }

        private void LateUpdate()
        {
            if (_dragging) return;
            onHover?.Invoke(_hoveredPart);

            Physics.SyncTransforms();

            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) { _hoveredPart = ""; return; }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _hoveredPart = "";
                return;
            }

            Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
                _hoveredPart = hit.collider != null ? hit.collider.name : "";
            else
                _hoveredPart = "";
        }

        public void playUIAudio(int index)
        {
            var audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource != null && UISounds != null && index >= 0 && index < UISounds.Count)
            {
                audioSource.clip = UISounds[index];
                audioSource.Play();
            }
        }

        public void SetActiveCharacter(int i)
        {
            if (CharacterParent == null) return;

            int childCount = CharacterParent.transform.childCount;
            if (childCount == 0) return;

            if (i < 0 || i >= childCount) i = 0;
            _currentCharacter = i;

            SavePlayerModelType(i);

            for (int j = 0; j < childCount; j++)
            {
                Transform tr = CharacterParent.transform.GetChild(j);
                if (tr == null) continue;

                GameObject character = tr.gameObject;
                if (character == null) continue;

                if (j == i)
                {
                    character.SetActive(true);
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

            int next = (_currentCharacter == count - 1) ? 0 : _currentCharacter + 1;
            SetActiveCharacter(next);
        }

        public void characterPrev()
        {
            if (CharacterParent == null) return;
            int count = CharacterParent.transform.childCount;
            if (count == 0) return;

            int prev = (_currentCharacter == 0) ? count - 1 : _currentCharacter - 1;
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
            }
            PlayerPrefs.SetString("PlayerModelType", val);
        }
    }
}
