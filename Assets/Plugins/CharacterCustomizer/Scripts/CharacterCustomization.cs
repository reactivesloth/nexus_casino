using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CC
{
    public class CharacterCustomization : MonoBehaviour
    {
        public string CharacterName;

        public SkinnedMeshRenderer MainMesh;
        public GameObject UI;
        private GameObject UI_Instance;
        public bool Autoload = false;
        public bool LoadAsync = false;

        public List<scrObj_Hair> HairTables = new List<scrObj_Hair>();
        private List<GameObject> HairObjects = new List<GameObject>();

        public List<scrObj_Apparel> ApparelTables = new List<scrObj_Apparel>();
        private List<GameObject> ApparelObjects = new List<GameObject>();

        public scrObj_Outfits Outfits;
        public scrObj_Randomizer Randomizer;

        public scrObj_Presets Presets;
        public CC_CharacterData StoredCharacterData;

        private string SavePath
        {
            get
            {
#if UNITY_EDITOR
                return Application.dataPath + "/CharacterCustomizer.json";
#else
                return Application.persistentDataPath + "/CharacterCustomizer.json";
#endif
            }
        }

        public delegate void OnCharacterLoaded(CharacterCustomization script);
        public event OnCharacterLoaded onCharacterLoaded;

        private int lastHoverIndex = 0;
        private Coroutine activeCoroutine;
        [SerializeField] private bool initializeOnStartInsteadOfAwake;

        #region Initialize script
        private void Awake()
        {
            if (!initializeOnStartInsteadOfAwake)
                InitializeScript();
        }

        private void Start()
        {
            if (initializeOnStartInsteadOfAwake)
                InitializeScript();
        }

        private void InitializeScript()
        {
            var meshesAll = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < meshesAll.Length; i++)
            {
                var m = meshesAll[i];
                if (m != null) m.gameObject.SetActive(true);
            }

            if (CC_UI_Manager.instance != null)
            {
                CC_UI_Manager.instance.onHover += OnPartHovered;
                CC_UI_Manager.instance.onDrag += OnPartDragged;
            }

            Initialize();
        }

        private void OnPartDragged(string partX, string partY, float deltaX, float deltaY, bool first, bool last)
        {
            if (first) OnPartHovered("");
        }

        private void OnPartHovered(string hoveredPart)
        {
            int hoverIndex = 0;
            if (hoveredPart == "") hoverIndex = 0;
            else if (hoveredPart.Contains("spine_05")) hoverIndex = 1;
            else if (hoveredPart.Contains("spine")) hoverIndex = 2;
            else if (hoveredPart.Contains("pelvis")) hoverIndex = 3;
            else if (hoveredPart.Contains("lowerarm")) hoverIndex = 4;
            else if (hoveredPart.Contains("upperarm")) hoverIndex = 5;
            else if (hoveredPart.Contains("thigh")) hoverIndex = 6;
            else if (hoveredPart.Contains("calf")) hoverIndex = 7;
            else if (hoveredPart.Contains("head")) hoverIndex = 8;
            else if (hoveredPart.Contains("neck")) hoverIndex = 18;
            else if (hoveredPart.Contains("collider_nose")) hoverIndex = 12;
            else if (hoveredPart.Contains("collider_mouth")) hoverIndex = 13;
            else if (hoveredPart.Contains("collider_cheeks")) hoverIndex = 14;
            else if (hoveredPart.Contains("collider_cheekbones")) hoverIndex = 15;
            else if (hoveredPart.Contains("collider_jaw")) hoverIndex = 16;
            else if (hoveredPart.Contains("collider_chin")) hoverIndex = 17;
            else if (hoveredPart.Contains("collider_eye")) hoverIndex = 19;
            else if (hoveredPart.Contains("collider_brow")) hoverIndex = 20;

            if (hoverIndex != lastHoverIndex)
            {
                setFloatProperty(new CC_Property { propertyName = "_HoverSamplePoint", floatValue = hoverIndex });
                if (hoverIndex == 1) setFloatProperty(new CC_Property { propertyName = "_HoverSamplePoint", floatValue = 11, meshTag = "Head" });
                lastHoverIndex = hoverIndex;
            }
        }

        public void Initialize()
        {
            var toDelete = GetComponentsInChildren<DeleteOnStart>();
            for (int i = 0; i < toDelete.Length; i++)
            {
                if (toDelete[i] != null)
                    Destroy(toDelete[i].gameObject);
            }

            var meshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < meshes.Length; i++)
            {
                var mesh = meshes[i];
                if (mesh == null) continue;

                if (mesh.gameObject.GetComponent<BlendshapeManager>() == null)
                    mesh.gameObject.AddComponent<BlendshapeManager>().parseBlendshapes();

                if (UI != null)
                {
                    var mats = mesh.materials; // material instances (expected behavior for per-instance edits)
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (mat == null) continue;
                        // avoid LINQ Contains()
                        var kws = mat.shader.keywordSpace.keywordNames;
                        bool hasCustomization = false;
                        for (int k = 0; k < kws.Length; k++)
                        {
                            if (kws[k] == "_CUSTOMIZATION") { hasCustomization = true; break; }
                        }
                        if (hasCustomization)
                            mat.SetKeyword(new UnityEngine.Rendering.LocalKeyword(mat.shader, "_CUSTOMIZATION"), true);
                    }
                }
            }

            HairObjects = new List<GameObject>(new GameObject[HairTables.Count]);
            ApparelObjects = new List<GameObject>(new GameObject[ApparelTables.Count]);

            if (Autoload) LoadFromJSON();

            if (UI != null && CC_UI_Manager.instance != null)
            {
                var physicsManager = GetComponentInChildren<PhysicsManager>();
                if (physicsManager != null) physicsManager.customizationSetup();

                if (UI_Instance == null)
                {
                    UI_Instance = Instantiate(UI, CC_UI_Manager.instance.transform);
                    var util = UI_Instance != null ? UI_Instance.GetComponent<CC_UI_Util>() : null;
                    if (util == null)
                    {
                        Debug.LogError("UI is missing CC_UI_Util script");
                        return;
                    }
                    util.Initialize(this);
                }
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
        #endregion

        public void SwitchHead(bool value)
        {
            if (MainMesh != null) MainMesh.enabled = value;
            if (HairObjects != null && HairObjects.Count > 0)
            {
                for (int i = 0; i < HairObjects.Count; i++)
                {
                    var o = HairObjects[i];
                    if (o == null) continue;
                    var r = o.GetComponentInChildren<Renderer>();
                    if (r != null) r.enabled = value;
                }
            }
        }

        #region Save & Load
        public void SaveToJSON(string name = null)
        {
            if (!File.Exists(SavePath)) createSaveFile();

            if (string.IsNullOrEmpty(name)) name = CharacterName;
            if (!string.IsNullOrEmpty(name))
            {
                string jsonLoad = File.ReadAllText(SavePath);
                CC_SaveData CC_SaveData = JsonUtility.FromJson<CC_SaveData>(jsonLoad);

                // clone data
                string characterDataJSON = JsonUtility.ToJson(StoredCharacterData, true);
                var characterDataCopy = JsonUtility.FromJson<CC_CharacterData>(characterDataJSON);
                characterDataCopy.CharacterName = name;
                characterDataCopy.CharacterPrefab = gameObject.name;

                int index = -1;
                for (int i = 0; i < CC_SaveData.SavedCharacters.Count; i++)
                {
                    if (CC_SaveData.SavedCharacters[i].CharacterName == name) { index = i; break; }
                }

                if (index != -1) CC_SaveData.SavedCharacters[index] = characterDataCopy;
                else CC_SaveData.SavedCharacters.Add(characterDataCopy);

                string jsonSave = JsonUtility.ToJson(CC_SaveData, true);
                File.WriteAllText(SavePath, jsonSave);

                ApplyCharacterVars(StoredCharacterData);
            }
        }

        public void InstantiateCharacter(string name, Transform _transform)
        {
            if (!File.Exists(SavePath)) createSaveFile();

            string jsonLoad = File.ReadAllText(SavePath);
            var CC_SaveData = JsonUtility.FromJson<CC_SaveData>(jsonLoad);

            int index = -1;
            for (int i = 0; i < CC_SaveData.SavedCharacters.Count; i++)
            {
                if (CC_SaveData.SavedCharacters[i].CharacterName == name) { index = i; break; }
            }

            if (index != -1)
            {
                var prefabPath = CC_SaveData.SavedCharacters[index].CharacterPrefab;
                var loaded = Resources.Load(prefabPath);
                if (loaded != null)
                {
                    var newCharacter = (GameObject)Instantiate(loaded, _transform);
                    var cc = newCharacter.GetComponent<CharacterCustomization>();
                    if (cc != null)
                    {
                        cc.CharacterName = name;
                        cc.Initialize();
                    }
                }
            }
        }

        public void SaveToPrefab()
        {
#if UNITY_EDITOR
            string characterDataJSON = JsonUtility.ToJson(StoredCharacterData, true);
            var characterDataCopy = JsonUtility.FromJson<CC_CharacterData>(characterDataJSON);

            var ogPrefab = Resources.Load(StoredCharacterData.CharacterPrefab);
            if (ogPrefab == null) throw new Exception("Prefab not assigned in character data");
            var newPrefab = (GameObject)PrefabUtility.InstantiatePrefab(ogPrefab);

            if (File.Exists(SavePath))
            {
                string jsonLoad = File.ReadAllText(SavePath);
                var CC_SaveData = JsonUtility.FromJson<CC_SaveData>(jsonLoad);
                int index = -1;
                for (int i = 0; i < CC_SaveData.SavedCharacters.Count; i++)
                {
                    if (CC_SaveData.SavedCharacters[i].CharacterName == StoredCharacterData.CharacterName) { index = i; break; }
                }
                if (index != -1)
                {
                    CC_SaveData.SavedCharacters.RemoveAt(index);
                    string jsonSave = JsonUtility.ToJson(CC_SaveData, true);
                    File.WriteAllText(SavePath, jsonSave);
                }
            }

            string prefabSuffix = "_" + CharacterName;
            characterDataCopy.CharacterName = CharacterName;

            string prefabPath = AssetDatabase.GetAssetPath(ogPrefab);
            string newPath = prefabPath.Replace(".prefab", prefabSuffix + ".prefab");
            var cc = newPrefab.GetComponent<CharacterCustomization>();
            if (cc != null)
            {
                cc.CharacterName = CharacterName;
                cc.Autoload = true;
            }
            PrefabUtility.SaveAsPrefabAsset(newPrefab, newPath);
            if (Presets != null)
            {
                int presetIndex = -1;
                for (int i = 0; i < Presets.Presets.Count; i++)
                {
                    if (Presets.Presets[i].CharacterName == characterDataCopy.CharacterName) { presetIndex = i; break; }
                }
                if (presetIndex != -1) Presets.Presets[presetIndex] = characterDataCopy;
                else Presets.Presets.Add(characterDataCopy);
            }
            DestroyImmediate(newPrefab);
#endif
        }

        public void SaveToPreset(string presetName)
        {
#if UNITY_EDITOR
            string characterDataJSON = JsonUtility.ToJson(StoredCharacterData, true);
            var characterDataCopy = JsonUtility.FromJson<CC_CharacterData>(characterDataJSON);
            characterDataCopy.CharacterName = presetName;

            if (Presets != null)
            {
                int presetIndex = -1;
                for (int i = 0; i < Presets.Presets.Count; i++)
                {
                    if (Presets.Presets[i].CharacterName == presetName) { presetIndex = i; break; }
                }
                if (presetIndex != -1) Presets.Presets[presetIndex] = characterDataCopy;
                else Presets.Presets.Add(characterDataCopy);
            }
#endif
        }

        public void LoadFromJSON(string jsonString = "")
        {
            if (!File.Exists(SavePath))
            {
                createSaveFile();
            }

            if (!string.IsNullOrEmpty(CharacterName))
            {
                string jsonLoad = File.ReadAllText(SavePath);
                if (!string.IsNullOrEmpty(jsonString)) jsonLoad = jsonString;
                CC_SaveData CC_SaveData = JsonUtility.FromJson<CC_SaveData>(jsonLoad);

                // find by name
                StoredCharacterData = null;
                for (int i = 0; i < CC_SaveData.SavedCharacters.Count; i++)
                {
                    if (CC_SaveData.SavedCharacters[i].CharacterName == CharacterName)
                    {
                        StoredCharacterData = CC_SaveData.SavedCharacters[i];
                        break;
                    }
                }

                if (StoredCharacterData == null)
                {
                    randomizeAll();
                    randomizeCharacterAndOutfit();
                }

                ApplyCharacterVars(StoredCharacterData);
            }
        }

        public string GetJSON()
        {
            if (!File.Exists(SavePath)) createSaveFile();
            return !string.IsNullOrEmpty(CharacterName) ? File.ReadAllText(SavePath) : string.Empty;
        }

        public bool LoadFromPreset(string presetName)
        {
            if (GetPresetData(presetName, out var preset))
            {
                StoredCharacterData = JsonUtility.FromJson<CC_CharacterData>(JsonUtility.ToJson(preset));
                StoredCharacterData.CharacterName = CharacterName;
                ApplyCharacterVars(StoredCharacterData);
                return true;
            }
            return false;
        }

        public bool GetPresetData(string presetName, out CC_CharacterData preset)
        {
            preset = null;
            if (Presets != null)
            {
                for (int i = 0; i < Presets.Presets.Count; i++)
                {
                    if (Presets.Presets[i].CharacterName == presetName)
                    {
                        preset = Presets.Presets[i];
                        break;
                    }
                }
                if (preset == null && Presets.Presets.Count > 0) preset = Presets.Presets[0];
            }
            return preset != null;
        }

        private void EnsureCharacterData(ref CC_CharacterData characterData)
        {
            if (characterData == null)
            {
                characterData = new CC_CharacterData
                {
                    CharacterName = CharacterName,
                    CharacterPrefab = gameObject.name,
                    Blendshapes = new List<CC_Property>(),
                    HairNames = new List<string>(),
                    ApparelNames = new List<string>(),
                    ApparelMaterials = new List<int>(),
                    FloatProperties = new List<CC_Property>(),
                    TextureProperties = new List<CC_Property>(),
                    ColorProperties = new List<CC_Property>()
                };
            }
        }

        public void ApplyCharacterVars(CC_CharacterData characterData)
        {
            EnsureCharacterData(ref characterData);
            StoredCharacterData = characterData;

            while (StoredCharacterData.HairNames.Count < HairObjects.Count) StoredCharacterData.HairNames.Add("");
            while (StoredCharacterData.ApparelNames.Count < ApparelObjects.Count) StoredCharacterData.ApparelNames.Add("");
            while (StoredCharacterData.ApparelMaterials.Count < ApparelObjects.Count) StoredCharacterData.ApparelMaterials.Add(0);

            for (int i = 0; i < characterData.Blendshapes.Count; i++)
            {
                var bs = characterData.Blendshapes[i];
                setBlendshapeByName(bs.propertyName, bs.floatValue, false);
            }

            for (int i = 0; i < characterData.HairNames.Count; i++) setHairByName(characterData.HairNames[i], i);
            for (int i = 0; i < characterData.ApparelNames.Count; i++) setApparelByName(characterData.ApparelNames[i], i, characterData.ApparelMaterials[i]);

            for (int i = 0; i < characterData.TextureProperties.Count; i++) setTextureProperty(characterData.TextureProperties[i], false);
            for (int i = 0; i < characterData.FloatProperties.Count; i++) setFloatProperty(characterData.FloatProperties[i], false);
            for (int i = 0; i < characterData.ColorProperties.Count; i++) setColorProperty(characterData.ColorProperties[i], false);

            if (UI_Instance != null)
            {
                var util = UI_Instance.GetComponent<CC_UI_Util>();
                if (util != null) util.refreshUI();
            }
            onCharacterLoaded?.Invoke(this);
        }

        public IEnumerator ApplyCharacterVarsAsync(CC_CharacterData characterData)
        {
            EnsureCharacterData(ref characterData);
            StoredCharacterData = characterData;

            while (StoredCharacterData.HairNames.Count < HairObjects.Count) StoredCharacterData.HairNames.Add("");
            while (StoredCharacterData.ApparelNames.Count < ApparelObjects.Count) StoredCharacterData.ApparelNames.Add("");
            while (StoredCharacterData.ApparelMaterials.Count < ApparelObjects.Count) StoredCharacterData.ApparelMaterials.Add(0);

            for (int i = 0; i < characterData.Blendshapes.Count; i++)
            {
                var bs = characterData.Blendshapes[i];
                setBlendshapeByName(bs.propertyName, bs.floatValue, false);
                if (i % 5 == 0) yield return null;
            }

            for (int i = 0; i < characterData.HairNames.Count; i++) { setHairByName(characterData.HairNames[i], i); yield return null; }
            for (int i = 0; i < characterData.ApparelNames.Count; i++) { setApparelByName(characterData.ApparelNames[i], i, characterData.ApparelMaterials[i]); yield return null; }
            for (int i = 0; i < characterData.TextureProperties.Count; i++) { setTextureProperty(characterData.TextureProperties[i], false); yield return null; }
            for (int i = 0; i < characterData.FloatProperties.Count; i++) { setFloatProperty(characterData.FloatProperties[i], false); yield return null; }
            for (int i = 0; i < characterData.ColorProperties.Count; i++) { setColorProperty(characterData.ColorProperties[i], false); yield return null; }

            if (UI_Instance != null)
            {
                var util = UI_Instance.GetComponent<CC_UI_Util>();
                if (util != null) util.refreshUI();
            }
            onCharacterLoaded?.Invoke(this);
        }

        public void createSaveFile()
        {
            string json = JsonUtility.ToJson(new CC_SaveData(), true);
            File.WriteAllText(SavePath, json);
        }

        public void setCharacterName(string newName)
        {
            CharacterName = newName;
            if (StoredCharacterData != null) StoredCharacterData.CharacterName = newName;
        }
        #endregion

        #region Customization
        public void setHair(int selection, int slot)
        {
            if (slot >= HairTables.Count) { Debug.LogError("Tried to set hair from non-existing hair table"); return; }
            var table = HairTables[slot];
            if (selection >= table.Hairstyles.Count) return;

            var HairData = table.Hairstyles[selection];

            if (HairObjects[slot] != null) Destroy(HairObjects[slot]);

            if (HairData.Mesh != null)
            {
                HairObjects[slot] = Instantiate(HairData.Mesh, transform);
                var HairObject = HairObjects[slot];

                var skinned = HairObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < skinned.Length; i++)
                {
                    var mesh = skinned[i];
                    if (mesh == null) continue;
                    var manager = mesh.gameObject.AddComponent<BlendshapeManager>();
                    manager.parseBlendshapes();
                    for (int s = 0; s < StoredCharacterData.Blendshapes.Count; s++)
                    {
                        var shapeData = StoredCharacterData.Blendshapes[s];
                        manager.setBlendshape(shapeData.propertyName, shapeData.floatValue);
                    }
                }

                if (HairData.AddCopyPoseScript)
                {
                    HairObject.AddComponent<CopyPose>();
                }
                else if (MainMesh != null && MainMesh.rootBone != null)
                {
                    // bone map without LINQ
                    var mainTransforms = MainMesh.rootBone.GetComponentsInChildren<Transform>();
                    var boneMap = new Dictionary<string, Transform>(mainTransforms.Length);
                    for (int i = 0; i < mainTransforms.Length; i++)
                    {
                        var t = mainTransforms[i]; if (t == null) continue;
                        if (!boneMap.ContainsKey(t.name)) boneMap.Add(t.name, t);
                    }

                    for (int i = 0; i < skinned.Length; i++)
                    {
                        var mesh = skinned[i];
                        if (mesh == null) continue;
                        var oldRoot = mesh.rootBone;
                        var newBones = new Transform[mesh.bones.Length];
                        for (int b = 0; b < mesh.bones.Length; b++)
                        {
                            var ob = mesh.bones[b];
                            if (ob == null) { newBones[b] = null; continue; }
                            if (!boneMap.TryGetValue(ob.name, out newBones[b])) newBones[b] = null;
                        }
                        if (oldRoot != null) Destroy(oldRoot.gameObject);
                        mesh.bones = newBones;
                        mesh.rootBone = MainMesh.rootBone;
                        mesh.localBounds = MainMesh.localBounds;
                    }

                    var lodGroup = HairObject.GetComponentInChildren<LODGroup>();
                    if (lodGroup != null) { lodGroup.RecalculateBounds(); lodGroup.size = 0.5f; }
                }
            }

            var shadowMapProperty = table.SkinShadowMapProperty;
            if (!string.IsNullOrEmpty(shadowMapProperty.propertyName) && HairData.ShadowMap != null)
                setTextureProperty(shadowMapProperty, false, HairData.ShadowMap);

            setColorProperty(table.HairTintProperty, false);

            StoredCharacterData.HairNames[slot] = HairData.Name;
        }

        public void setHairByName(string name, int slot)
        {
            if (slot >= HairTables.Count) return;
            var list = HairTables[slot].Hairstyles;
            int index = -1;
            for (int i = 0; i < list.Count; i++) if (list[i].Name == name) { index = i; break; }
            if (index != -1) setHair(index, slot);
        }

        public void setApparel(int selection, int slot, int materialSelection)
        {
            if (slot >= ApparelTables.Count) { Debug.LogError("Tried to set apparel from non-existing apparel table"); return; }
            var table = ApparelTables[slot];
            if (selection >= table.Items.Count) return;

            var ApparelData = table.Items[selection];

            if (ApparelObjects[slot] != null) Destroy(ApparelObjects[slot]);

            if (ApparelData.Mesh != null)
            {
                ApparelObjects[slot] = Instantiate(ApparelData.Mesh, transform);
                var ApparelObject = ApparelObjects[slot];

                var skinned = ApparelObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int i = 0; i < skinned.Length; i++)
                {
                    var mesh = skinned[i];
                    if (mesh == null) continue;
                    var manager = mesh.gameObject.AddComponent<BlendshapeManager>();
                    manager.parseBlendshapes();
                    for (int s = 0; s < StoredCharacterData.Blendshapes.Count; s++)
                    {
                        var shapeData = StoredCharacterData.Blendshapes[s];
                        manager.setBlendshape(shapeData.propertyName, shapeData.floatValue);
                    }
                }

                // set tints
                for (int i = 0; i < skinned.Length; i++)
                {
                    var mesh = skinned[i]; if (mesh == null) continue;
                    if (materialSelection >= ApparelData.Materials.Count) break;
                    var defs = ApparelData.Materials[materialSelection].MaterialDefinitions;
                    var mats = mesh.materials;
                    int count = mats.Length < defs.Count ? mats.Length : defs.Count;
                    for (int m = 0; m < count; m++)
                    {
                        var mat = mats[m]; var def = defs[m];
                        if (mat == null) continue;
                        mat.SetColor("_Tint", def.MainTint);
                        mat.SetColor("_Tint_R", def.TintR);
                        mat.SetColor("_Tint_G", def.TintG);
                        mat.SetColor("_Tint_B", def.TintB);
                        mat.SetTexture("_Print", def.Print != null ? def.Print : Resources.Load<Texture2D>("T_Transparent"));
                    }
                }

                if (ApparelData.AddCopyPoseScript)
                {
                    ApparelObject.AddComponent<CopyPose>();
                }
                else if (MainMesh != null && MainMesh.rootBone != null)
                {
                    var mainTransforms = MainMesh.rootBone.GetComponentsInChildren<Transform>();
                    var boneMap = new Dictionary<string, Transform>(mainTransforms.Length);
                    for (int i = 0; i < mainTransforms.Length; i++)
                    {
                        var t = mainTransforms[i]; if (t == null) continue;
                        if (!boneMap.ContainsKey(t.name)) boneMap.Add(t.name, t);
                    }

                    for (int i = 0; i < skinned.Length; i++)
                    {
                        var mesh = skinned[i]; if (mesh == null) continue;
                        var oldRoot = mesh.rootBone;
                        var newBones = new Transform[mesh.bones.Length];
                        for (int b = 0; b < mesh.bones.Length; b++)
                        {
                            var ob = mesh.bones[b];
                            if (ob == null) { newBones[b] = null; continue; }
                            if (!boneMap.TryGetValue(ob.name, out newBones[b])) newBones[b] = null;
                        }
                        if (oldRoot != null) Destroy(oldRoot.gameObject);
                        mesh.bones = newBones;
                        mesh.rootBone = MainMesh.rootBone;
                        mesh.localBounds = MainMesh.localBounds;
                    }

                    var lodGroup = ApparelObject.GetComponentInChildren<LODGroup>();
                    if (lodGroup != null) { lodGroup.RecalculateBounds(); lodGroup.size = 0.5f; }
                }
            }

            if (ApparelData.FootOffset.HeightOffset >= 0)
            {
                setBodyCustomization("BodyCustomization_FootRotation", ApparelData.FootOffset.FootRotation);
                setBodyCustomization("BodyCustomization_BallRotation", ApparelData.FootOffset.BallRotation);
                setBodyCustomization("BodyCustomization_HeightOffset", ApparelData.FootOffset.HeightOffset);
            }

            if (ApparelData.NeckShrink >= 0)
            {
                setFloatProperty(new CC_Property { propertyName = "_Neck_Shrink", materialIndex = 0, meshTag = "Head", floatValue = ApparelData.NeckShrink / 100f }, false);
            }

            setTextureProperty(table.SkinMaskProperty, false, ApparelData.Mask);

            StoredCharacterData.ApparelNames[slot] = ApparelData.Name;
            StoredCharacterData.ApparelMaterials[slot] = materialSelection;
        }

        public void setApparelByName(string name, int slot, int materialSelection)
        {
            if (slot >= ApparelTables.Count) return;
            var items = ApparelTables[slot].Items;
            int index = -1;
            for (int i = 0; i < items.Count; i++) if (items[i].Name == name) { index = i; break; }
            if (index != -1) setApparel(index, slot, materialSelection);
        }

        public void setRandomOutfit()
        {
            if (Outfits != null && Outfits.GetRandomOutfit(this, out var apparelOptions, out var apparelMaterials))
            {
                if (activeCoroutine != null) StopCoroutine(activeCoroutine);
                activeCoroutine = StartCoroutine(setRandomOutfitAsync());

                IEnumerator setRandomOutfitAsync()
                {
                    for (int i = 0; i < apparelOptions.Count; i++)
                    {
                        setApparelByName(apparelOptions[i], i, apparelMaterials[i]);
                        if (LoadAsync) yield return null;
                    }
                }
            }
        }

        public void randomizeAll()
        {
            if (Randomizer == null) return;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(Randomizer.randomizeAll(this));
            if (UI_Instance != null) { var u = UI_Instance.GetComponent<CC_UI_Util>(); if (u != null) u.refreshUI(); }
        }

        public void randomizeCharacterAndOutfit()
        {
            if (Randomizer == null || Outfits == null) return;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(doRandomize());

            IEnumerator doRandomize()
            {
                yield return Randomizer.randomizeAll(this);
                setRandomOutfit();
            }
        }

        public void setBlendshapeByName(string name, float value, bool save = true)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (save) saveProperty(ref StoredCharacterData.Blendshapes, new CC_Property { propertyName = name, floatValue = value });
            if (name.Contains("BodyCustomization")) { setBodyCustomization(name, value); return; }

            var managers = GetComponentsInChildren<BlendshapeManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                var m = managers[i]; if (m != null) m.setBlendshape(name, value);
            }
        }

        public void setBodyCustomization(string name, float value)
        {
            var modifyBoneManager = GetComponentInChildren<ModifyBone_Manager>();
            if (modifyBoneManager != null) modifyBoneManager.setModifyValue(name, value);
        }

        // Optimized setters to avoid building large temp lists
        public void setTextureProperty(CC_Property p, bool save = false, Texture2D t = null)
        {
            if (t != null) p.stringValue = t.name;
            var renderers = string.IsNullOrEmpty(p.meshTag) ? GetComponentsInChildren<Renderer>().ToList() : getMeshByTag(p.meshTag);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i]; if (r == null) continue;
                var mats = r.materials;
                if (p.materialIndex >= 0)
                {
                    if (p.materialIndex < mats.Length)
                    {
                        var mat = mats[p.materialIndex];
                        if (mat != null && mat.HasProperty(p.propertyName)) mat.SetTexture(p.propertyName, t != null ? t : Resources.Load<Texture2D>(p.stringValue));
                    }
                }
                else
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m]; if (mat == null) continue;
                        if (mat.HasProperty(p.propertyName)) mat.SetTexture(p.propertyName, t != null ? t : Resources.Load<Texture2D>(p.stringValue));
                    }
                }
            }
            if (save) saveProperty(ref StoredCharacterData.TextureProperties, p);
        }

        public void setFloatProperty(CC_Property p, bool save = false)
        {
            var renderers = string.IsNullOrEmpty(p.meshTag) ? GetComponentsInChildren<Renderer>().ToList() : getMeshByTag(p.meshTag);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i]; if (r == null) continue;
                var mats = r.materials;
                if (p.materialIndex >= 0)
                {
                    if (p.materialIndex < mats.Length)
                    {
                        var mat = mats[p.materialIndex];
                        if (mat != null && mat.HasProperty(p.propertyName)) mat.SetFloat(p.propertyName, p.floatValue);
                    }
                }
                else
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m]; if (mat == null) continue;
                        if (mat.HasProperty(p.propertyName)) mat.SetFloat(p.propertyName, p.floatValue);
                    }
                }
            }
            if (save) saveProperty(ref StoredCharacterData.FloatProperties, p);
        }

        public void setColorProperty(CC_Property p, bool save = false)
        {
            var renderers = string.IsNullOrEmpty(p.meshTag) ? GetComponentsInChildren<Renderer>().ToList() : getMeshByTag(p.meshTag);
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i]; if (r == null) continue;
                var mats = r.materials;
                if (p.materialIndex >= 0)
                {
                    if (p.materialIndex < mats.Length)
                    {
                        var mat = mats[p.materialIndex];
                        if (mat != null && mat.HasProperty(p.propertyName)) mat.SetColor(p.propertyName, p.colorValue);
                    }
                }
                else
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m]; if (mat == null) continue;
                        if (mat.HasProperty(p.propertyName)) mat.SetColor(p.propertyName, p.colorValue);
                    }
                }
            }
            if (save) saveProperty(ref StoredCharacterData.ColorProperties, p);
        }

        public List<Renderer> getMeshByTag(string tag)
        {
            var all = GetComponentsInChildren<Renderer>();
            var list = new List<Renderer>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i]; if (r != null && r.gameObject.tag == tag) list.Add(r);
            }
            return list;
        }

        public bool findProperty(List<CC_Property> properties, CC_Property p, out CC_Property pOut, out int index)
        {
            index = -1;
            for (int i = 0; i < properties.Count; i++)
            {
                var it = properties[i];
                if (it.propertyName == p.propertyName && it.materialIndex == p.materialIndex && it.meshTag == p.meshTag)
                { index = i; break; }
            }
            if (index >= 0)
            {
                pOut = properties[index];
                return true;
            }
            pOut = p;
            return false;
        }

        public void saveProperty(ref List<CC_Property> properties, CC_Property p)
        {
            int index = -1;
            for (int i = 0; i < properties.Count; i++)
            {
                var it = properties[i];
                if (it.materialIndex == p.materialIndex && it.propertyName == p.propertyName && it.meshTag == p.meshTag)
                { index = i; break; }
            }
            if (index == -1) properties.Add(p); else properties[index] = p;
        }
        #endregion

#if UNITY_EDITOR
        [CustomEditor(typeof(CharacterCustomization))]
        public class CharacterSelectorEditor : Editor
        {
            private SerializedProperty characterNameProp;

            private void OnEnable()
            {
                characterNameProp = serializedObject.FindProperty("CharacterName");
            }

            public override void OnInspectorGUI()
            {
                CharacterCustomization characterSelector = (CharacterCustomization)target;
                serializedObject.Update();

                if (characterSelector.Presets != null && characterSelector.Presets.Presets.Count > 0)
                {
                    var presets = characterSelector.Presets.Presets;
                    string[] characterNames = new string[presets.Count];
                    for (int i = 0; i < presets.Count; i++) characterNames[i] = presets[i].CharacterName;

                    int oldIndex = ArrayUtility.IndexOf(characterNames, characterNameProp.stringValue);
                    int newIndex = EditorGUILayout.Popup(oldIndex, characterNames);

                    if (newIndex != oldIndex && newIndex >= 0 && newIndex < characterNames.Length)
                    {
                        characterNameProp.stringValue = presets[newIndex].CharacterName;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Please assign a Presets ScriptableObject.", MessageType.Warning);
                }

                serializedObject.ApplyModifiedProperties();
                DrawDefaultInspector();
            }
        }
#endif
    }
}
