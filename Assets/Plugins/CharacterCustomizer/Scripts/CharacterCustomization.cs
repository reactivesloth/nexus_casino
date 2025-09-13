using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;

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

        public List<scrObj_Hair> HairTables = new List<scrObj_Hair>(); //Available hair prefabs

        private List<GameObject> HairObjects = new List<GameObject>(); //Active hair prefabs

        public List<scrObj_Apparel> ApparelTables = new List<scrObj_Apparel>(); //Available apparel prefabs

        private List<GameObject> ApparelObjects = new List<GameObject>(); //Active apparel prefabs

        public List<string> DefaultApparel = new List<string>(); //Default apparel, same order as ApparelTables

        public scrObj_Outfits Outfits;
        public scrObj_Randomizer Randomizer;

        public scrObj_Presets Presets; //Available presets
        public CC_CharacterData StoredCharacterData; //Current character data

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

        //Event you can bind to notify when character has finished loading
        public delegate void OnCharacterLoaded(CharacterCustomization script);
        public event OnCharacterLoaded onCharacterLoaded;

        //Hover customization
        private int lastHoverIndex = 0;

        //Async loading
        private Coroutine activeCoroutine;
        [SerializeField] private bool initializeOnStartInsteadOfAwake;

        //Store character LOD size for hair/apparel bounds
        private float mainLODSize;

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
            foreach (var item in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                item.gameObject.SetActive(true);
            }

            if (CC_UI_Manager.instance != null)
            {
                CC_UI_Manager.instance.onHover += OnPartHovered;
                CC_UI_Manager.instance.onDrag += OnPartDragged;
            }

            mainLODSize = GetComponent<LODGroup>() == null ? 1.5f : GetComponentInChildren<LODGroup>().size;

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

        //Initializes this script - run on Start by default but you can run it whenever, see InstantiateCharacter for example
        public void Initialize()
        {
            foreach (var toDelete in GetComponentsInChildren<DeleteOnStart>())
            {
                Destroy(toDelete.gameObject);
            }

            foreach (var hairObject in HairObjects)
                if(hairObject != null)
                    Destroy(hairObject.gameObject);
            HairObjects.Clear();
            
            foreach (var apparelObject in ApparelObjects)
                if(apparelObject != null)
                    Destroy(apparelObject.gameObject);
            ApparelObjects.Clear();
            
            foreach (var mesh in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                //Add a blendshape manager script to every mesh
                if (mesh.gameObject.GetComponent<BlendshapeManager>() == null) mesh.gameObject.AddComponent<BlendshapeManager>().parseBlendshapes();

                //If UI prefab is valid
                if (UI != null)
                {
                    //Set Customization bool in material for hover effects etc
                    foreach (var material in mesh.materials)
                    {
                        if (material.shader.keywordSpace.keywordNames.Contains("_CUSTOMIZATION")) material.SetKeyword(new UnityEngine.Rendering.LocalKeyword(material.shader, "_CUSTOMIZATION"), true);
                    }
                }
            }

            //Initialize hair/apparel objects
            HairObjects = new List<GameObject>(new GameObject[HairTables.Count]);
            ApparelObjects = new List<GameObject>(new GameObject[ApparelTables.Count]);

            //Resize DefaultApparel
            while (DefaultApparel.Count < ApparelObjects.Count) DefaultApparel.Add("");

            //Load character
            if (Autoload) LoadFromJSON();

            if (UI != null && CC_UI_Manager.instance != null)
            {
                //Setup customization colliders for hover effects
                var physicsManager = GetComponentInChildren<PhysicsManager>();
                if (physicsManager != null)
                {
                    physicsManager.customizationSetup();
                }

                //Create UI
                UI_Instance = Instantiate(UI, CC_UI_Manager.instance.transform);
                if (UI_Instance.GetComponent<CC_UI_Util>() == null) { Debug.LogError("UI is missing CC_UI_Util script"); return; }
                UI_Instance.GetComponent<CC_UI_Util>().Initialize(this);
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

        #endregion Initialize script

        #region Save & Load
        public void SaveToJSON(string name = null)
        {
            //Create save file
            if (!File.Exists(SavePath)) createSaveFile();

            if (string.IsNullOrEmpty(name)) name = CharacterName;
            if (!string.IsNullOrEmpty(name))
            {
                //Load CC_SaveData from JSON file
                string jsonLoad = File.ReadAllText(SavePath);
                CC_SaveData CC_SaveData = JsonUtility.FromJson<CC_SaveData>(jsonLoad);

                //Clone character data
                string characterDataJSON = JsonUtility.ToJson(StoredCharacterData, true);
                var characterDataCopy = JsonUtility.FromJson<CC_CharacterData>(characterDataJSON);
                characterDataCopy.CharacterName = name;
                characterDataCopy.CharacterPrefab = gameObject.name;

                //Find character index by CharacterName
                int index = CC_SaveData.SavedCharacters.FindIndex(t => t.CharacterName == name);

                //If found, overwrite save data
                if (index != -1)
                {
                    CC_SaveData.SavedCharacters[index] = characterDataCopy;
                }
                //Otherwise add new character
                else
                {
                    CC_SaveData.SavedCharacters.Add(characterDataCopy);
                }

                //Save to JSON
                string jsonSave = JsonUtility.ToJson(CC_SaveData, true);
                File.WriteAllText(SavePath, jsonSave);
                    
                ApplyCharacterVars(StoredCharacterData);
            }
        }

        //Instantiate a character from name, not used anywhere but this is how you could do it
        public void InstantiateCharacter(string name, Transform _transform)
        {
            if (!File.Exists(SavePath)) createSaveFile();

            //Load CC_SaveData from JSON file
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

            //Clone character data
            string characterDataJSON = JsonUtility.ToJson(StoredCharacterData, true);
            var characterDataCopy = JsonUtility.FromJson<CC_CharacterData>(characterDataJSON);

            //Load original prefab to duplicate
            var ogPrefab = Resources.Load(StoredCharacterData.CharacterPrefab);
            if (ogPrefab == null) throw new System.Exception("Prefab not assigned in character data");
            var newPrefab = (GameObject)PrefabUtility.InstantiatePrefab(ogPrefab);

            //Delete character in JSON file
            if (File.Exists(SavePath))
            {
                //Load CC_SaveData from JSON file
                string jsonLoad = File.ReadAllText(SavePath);
                var CC_SaveData = JsonUtility.FromJson<CC_SaveData>(jsonLoad);
                int index = CC_SaveData.SavedCharacters.FindIndex(t => t.CharacterName == StoredCharacterData.CharacterName);
                if (index != -1)
                {
                    CC_SaveData.SavedCharacters.RemoveAt(index);
                    string jsonSave = JsonUtility.ToJson(CC_SaveData, true);
                    File.WriteAllText(SavePath, jsonSave);
                }
            }

            //Update name and prefab
            string prefabSuffix = "_" + CharacterName;
            characterDataCopy.CharacterName = CharacterName;

            //Create new prefab
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

            //Clone character data
            string characterDataJSON = JsonUtility.ToJson(StoredCharacterData, true);
            var characterDataCopy = JsonUtility.FromJson<CC_CharacterData>(characterDataJSON);
            characterDataCopy.CharacterName = presetName;

            //Overwrite or add new preset
            int presetIndex = Presets.Presets.FindIndex(t => t.CharacterName == presetName);
            if (presetIndex != -1)
            {
                Presets.Presets[presetIndex] = characterDataCopy;
            }
            else Presets.Presets.Add(characterDataCopy);

            EditorUtility.SetDirty(Presets);
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
            //Try to find a preset matching the character name
            preset = Presets.Presets.Find(t => t.CharacterName == presetName) ?? Presets.Presets.FirstOrDefault();
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
            
            //Start coroutine if async
            if (LoadAsync)
            {
                if (activeCoroutine != null) StopCoroutine(activeCoroutine);
                activeCoroutine = StartCoroutine(ApplyCharacterVarsAsync(characterData));
                return;
            }

            //Resize lists
            while (StoredCharacterData.HairNames.Count < HairObjects.Count)
            {
                StoredCharacterData.HairNames.Add("");
            }
            while (StoredCharacterData.ApparelNames.Count < ApparelObjects.Count)
            {
                StoredCharacterData.ApparelNames.Add("");
            }
            while (StoredCharacterData.ApparelMaterials.Count < ApparelObjects.Count)
            {
                StoredCharacterData.ApparelMaterials.Add(0);
            }

            //Set blendshapes
            for (int i = 0; i < characterData.Blendshapes.Count; i++)
            {
                setBlendshapeByName(characterData.Blendshapes[i].propertyName, characterData.Blendshapes[i].floatValue, false);
            }

            //Set hair
            for (int i = 0; i < characterData.HairNames.Count; i++)
            {
                setHairByName(characterData.HairNames[i], i);
            }

            //Set apparel
            for (int i = 0; i < characterData.ApparelNames.Count; i++)
            {
                setApparelByName(characterData.ApparelNames[i], i, characterData.ApparelMaterials[i]);
            }

            //Set texture properties
            foreach (var textureData in characterData.TextureProperties)
            {
                setTextureProperty(textureData, false);
            }

            //Set float properties
            foreach (var floatData in characterData.FloatProperties)
            {
                setFloatProperty(floatData, false);
            }

            //Set color properties
            foreach (var colorData in characterData.ColorProperties)
            {
                setColorProperty(colorData, false);
            }

            if (UI_Instance != null) UI_Instance.GetComponent<CC_UI_Util>().refreshUI();
            onCharacterLoaded?.Invoke(this);
        }

        public IEnumerator ApplyCharacterVarsAsync(CC_CharacterData characterData)
        {
            EnsureCharacterData(ref characterData);
            StoredCharacterData = characterData;
            
            //Create material instances
            var meshes = GetComponentsInChildren<Renderer>();
            var materials = new List<Material>();
            foreach (var mesh in meshes)
            {
                if (mesh == null) continue;
                foreach (var material in mesh.sharedMaterials)
                {
                    materials.Add(new Material(material));
                    yield return null;
                }
            }

            //Resize lists
            while (StoredCharacterData.HairNames.Count < HairObjects.Count)
            {
                StoredCharacterData.HairNames.Add("");
            }
            while (StoredCharacterData.ApparelNames.Count < ApparelObjects.Count)
            {
                StoredCharacterData.ApparelNames.Add("");
            }
            while (StoredCharacterData.ApparelMaterials.Count < ApparelObjects.Count)
            {
                StoredCharacterData.ApparelMaterials.Add(0);
            }

            //Set blendshapes
            for (int i = 0; i < characterData.Blendshapes.Count; i++)
            {
                setBlendshapeByName(characterData.Blendshapes[i].propertyName, characterData.Blendshapes[i].floatValue, false);
                if (i % 5 == 0) yield return null;
            }

            //Set hair
            for (int i = 0; i < characterData.HairNames.Count; i++)
            {
                setHairByName(characterData.HairNames[i], i);
                yield return null;
            }

            //Set apparel
            for (int i = 0; i < characterData.ApparelNames.Count; i++)
            {
                setApparelByName(characterData.ApparelNames[i], i, characterData.ApparelMaterials[i]);
                yield return null;
            }

            //Set texture properties
            foreach (var textureData in characterData.TextureProperties)
            {
                setTextureProperty(textureData, false);
                yield return null;
            }

            //Set float properties
            foreach (var floatData in characterData.FloatProperties)
            {
                setFloatProperty(floatData, false);
                yield return null;
            }

            //Set color properties
            foreach (var colorData in characterData.ColorProperties)
            {
                setColorProperty(colorData, false);
                yield return null;
            }

            if (UI_Instance != null) UI_Instance.GetComponent<CC_UI_Util>().refreshUI();
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
            if (slot >= HairTables.Count) Debug.LogError("Tried to set hair from non-existing hair table");

            if (HairTables[slot].Hairstyles.Count > selection)
            {
                scrObj_Hair.Hairstyle HairData = HairTables[slot].Hairstyles[selection];

                //Destroy active GameObject
                if (HairObjects[slot] != null) Destroy(HairObjects[slot]);

                //Set mesh if valid
                if (HairTables[slot].Hairstyles[selection].Mesh != null)
                {
                    HairObjects[slot] = Instantiate(HairData.Mesh, gameObject.transform);

                    var HairObject = HairObjects[slot];

                    //Add blendshape managers and update shapes
                    foreach (var mesh in HairObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var manager = mesh.gameObject.AddComponent<BlendshapeManager>();
                        manager.parseBlendshapes();
                        foreach (var shapeData in StoredCharacterData.Blendshapes)
                        {
                            manager.setBlendshape(shapeData.propertyName, shapeData.floatValue);
                        }
                    }

                    //Add CopyPose script
                    if (HairData.AddCopyPoseScript)
                    {
                        HairObject.AddComponent<CopyPose>();
                    }
                    //Otherwise assume hierarchy is the same
                    else
                    {
                        foreach (var mesh in HairObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var mainMeshTransforms = MainMesh.rootBone.GetComponentsInChildren<Transform>();
                            var mainMeshBoneMap = mainMeshTransforms.ToDictionary(t => t.name, t => t);

                            var mainMeshBones = new Transform[mesh.bones.Length];
                            var oldMeshRoot = mesh.rootBone;

                            //Map old bones to new bones
                            for (var i = 0; i < mesh.bones.Length; i++)
                            {
                                if (mesh.bones[i] == null) continue;
                                mainMeshBoneMap.TryGetValue(mesh.bones[i].name, out mainMeshBones[i]);
                            }

                            //Clean up old root and reassign properties
                            Destroy(oldMeshRoot.gameObject);
                            mesh.bones = mainMeshBones;
                            mesh.rootBone = MainMesh.rootBone;
                            mesh.localBounds = MainMesh.localBounds;
                        }

                        //Recalculate bounds
                        var lodGroup = HairObject.GetComponentInChildren<LODGroup>();
                        if (lodGroup != null) { lodGroup.RecalculateBounds(); lodGroup.size = mainLODSize; }
                    }
                }

                //Set shadow map
                var shadowMapProperty = HairTables[slot].SkinShadowMapProperty;
                if (shadowMapProperty.propertyName != "" && HairData.ShadowMap != null) setTextureProperty(shadowMapProperty, false, HairData.ShadowMap);

                //Update hair color
                if (findProperty(StoredCharacterData.ColorProperties, HairTables[slot].HairTintProperty, out var hairProperty, out int index))
                {
                    setColorProperty(hairProperty, false);
                }

                //Check hair compress values from apparel
                var equippedApparelData = getEquippedApparelData();
                foreach (var apparelData in equippedApparelData)
                {
                    if (apparelData == null) continue;

                    if (apparelData.CompressHair >= 0)
                    {
                        foreach (var hairObj in HairObjects)
                        {
                            if (hairObj == null) continue;
                            foreach (var blendshapeManager in hairObj.GetComponentsInChildren<BlendshapeManager>())
                            {
                                blendshapeManager.setBlendshape("compress", apparelData.CompressHair);
                            }
                        }
                    }
                }

                //Update hair name in StoredCharacterData
                StoredCharacterData.HairNames[slot] = HairData.Name;
            }
        }

        public void setHairByName(string name, int slot)
        {
            int index = HairTables[slot].Hairstyles.FindIndex(t => t.Name == name);
            if (index != -1) setHair(index, slot);
        }

        public void setApparel(int selection, int slot, int materialSelection)
        {
            if (slot >= ApparelTables.Count)
            {
                Debug.LogError("Tried to set apparel from non-existing apparel table");
                return;
            }

            if (selection >= ApparelTables[slot].Items.Count)
            {
                Debug.LogError("Apparel selection out of bounds for apparel table " + slot);
                return;
            }

            var ApparelData = ApparelTables[slot].Items[selection];

            #region Handle conflicting slots

            var slotsToReset = new HashSet<int>(); //Hash set to hold slots that will be reset to default
            var occupiedSlots = new HashSet<int>(ApparelData.HidesTheseSlots) { slot }; //Get this item's occupied slots
            var equippedApparelData = getEquippedApparelData(); //Get apparel data of all the currently equipped apparel

            //Check equipped apparel's slots against this item's occupied slots
            for (int i = 0; i < equippedApparelData.Count; i++)
            {
                if (equippedApparelData[i] == null) continue;
                var checkSlots = new HashSet<int>(equippedApparelData[i].HidesTheseSlots) { i };

                if (checkSlots.Overlaps(occupiedSlots)) //If there is any overlap, add all checked slots to the reset hash set
                {
                    slotsToReset.UnionWith(checkSlots);
                }
            }

            //Remove this item's slots (we don't want to reset the item we're currently equipping)
            slotsToReset.ExceptWith(occupiedSlots);

            //Temporarily set this slot's apparel name to empty to avoid being checked by slots that are to be reset
            StoredCharacterData.ApparelNames[slot] = "";

            //Reset slots to default apparel
            foreach (var slotToReset in slotsToReset)
            {
                StoredCharacterData.ApparelNames[slotToReset] = DefaultApparel[slotToReset]; //Store name ahead of time to avoid infinite loop
                setApparelByName(DefaultApparel[slotToReset], slotToReset, 0);
            }

            //Finally, delete hidden apparel
            foreach (var slotToHide in ApparelData.HidesTheseSlots)
            {
                //Delete name from stored data
                StoredCharacterData.ApparelNames[slotToHide] = "";
                //Destroy game object
                if (slotToHide < ApparelObjects.Count && ApparelObjects[slotToHide] != null)
                {
                    Destroy(ApparelObjects[slotToHide]);
                }
                //Reset mask
                setTextureProperty(ApparelTables[slotToHide].SkinMaskProperty, false, (Texture2D)Resources.Load("T_Flat_Black"));
                //Reset neck shrink
                if (equippedApparelData[slotToHide] != null && equippedApparelData[slotToHide].NeckShrink >= 0)
                {
                    setFloatProperty(new CC_Property() { propertyName = "_Neck_Shrink", materialIndex = 0, meshTag = "Head", floatValue = 0 });
                }
                //Reset foot transform
                if (equippedApparelData[slotToHide] != null && equippedApparelData[slotToHide].FootOffset.HeightOffset >= 0)
                {
                    setBodyCustomization("BodyCustomization_FootRotation", 0);
                    setBodyCustomization("BodyCustomization_BallRotation", 0);
                    setBodyCustomization("BodyCustomization_HeightOffset", 0);
                }
            }

            #endregion Handle conflicting slots

            //Destroy active GameObject
            if (ApparelObjects[slot] != null) Destroy(ApparelObjects[slot]);

            //Set mesh if valid
            if (ApparelTables[slot].Items[selection].Mesh != null)
            {
                ApparelObjects[slot] = Instantiate(ApparelData.Mesh, gameObject.transform);

                var ApparelObject = ApparelObjects[slot];

                //Add blendshape managers and update shapes
                foreach (var mesh in ApparelObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var manager = mesh.gameObject.AddComponent<BlendshapeManager>();
                    manager.parseBlendshapes();
                    foreach (var shapeData in StoredCharacterData.Blendshapes)
                    {
                        manager.setBlendshape(shapeData.propertyName, shapeData.floatValue);
                    }
                }

                //Set tints
                foreach (var mesh in ApparelObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (materialSelection >= ApparelData.Materials.Count) break;

                    var matDefinitions = ApparelData.Materials[materialSelection].MaterialDefinitions;

                    var baseMaterials = mesh.materials;

                    for (int i = 0; i < matDefinitions.Count; i++)
                    {
                        if (i >= baseMaterials.Length) break;

                        if (matDefinitions[i].MaterialOverride != null) baseMaterials[i] = new Material(matDefinitions[i].MaterialOverride);

                        baseMaterials[i].SetColor("_Tint", matDefinitions[i].MainTint);
                        baseMaterials[i].SetColor("_Tint_R", matDefinitions[i].TintR);
                        baseMaterials[i].SetColor("_Tint_G", matDefinitions[i].TintG);
                        baseMaterials[i].SetColor("_Tint_B", matDefinitions[i].TintB);
                        baseMaterials[i].SetTexture("_Print", matDefinitions[i].Print != null ? matDefinitions[i].Print : Resources.Load<Texture2D>("T_Transparent"));
                    }

                    mesh.materials = baseMaterials;
                }

                //Add CopyPose script
                if (ApparelData.AddCopyPoseScript)
                {
                    ApparelObject.AddComponent<CopyPose>();
                }
                //Otherwise assume hierarchy is the same
                else
                {
                    foreach (var mesh in ApparelObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mainMeshTransforms = MainMesh.rootBone.GetComponentsInChildren<Transform>();
                        var mainMeshBoneMap = mainMeshTransforms.ToDictionary(t => t.name, t => t);

                        var mainMeshBones = new Transform[mesh.bones.Length];
                        var oldMeshRoot = mesh.rootBone;

                        //Map old bones to new bones
                        for (var i = 0; i < mesh.bones.Length; i++)
                        {
                            if (mesh.bones[i] == null) continue;
                            mainMeshBoneMap.TryGetValue(mesh.bones[i].name, out mainMeshBones[i]);
                        }

                        //Clean up old root and reassign properties
                        Destroy(oldMeshRoot.gameObject);
                        mesh.bones = mainMeshBones;
                        mesh.rootBone = MainMesh.rootBone;
                        mesh.localBounds = MainMesh.localBounds;
                    }

                    //Recalculate bounds
                    var lodGroup = ApparelObject.GetComponentInChildren<LODGroup>();
                    if (lodGroup != null) { lodGroup.RecalculateBounds(); lodGroup.size = mainLODSize; }
                }
            }

            //Set foot offset
            if (ApparelData.FootOffset.HeightOffset >= 0)
            {
                setBodyCustomization("BodyCustomization_FootRotation", ApparelData.FootOffset.FootRotation);
                setBodyCustomization("BodyCustomization_BallRotation", ApparelData.FootOffset.BallRotation);
                setBodyCustomization("BodyCustomization_HeightOffset", ApparelData.FootOffset.HeightOffset);
            }

            //Set neck shrink
            if (ApparelData.NeckShrink >= 0) setFloatProperty(new CC_Property() { propertyName = "_Neck_Shrink", materialIndex = 0, meshTag = "Head", floatValue = ApparelData.NeckShrink / 100 });

            //Set hair compress
            if (ApparelData.CompressHair >= 0)
            {
                foreach (var hairObj in HairObjects)
                {
                    if (hairObj == null) continue;
                    foreach (var blendshapeManager in hairObj.GetComponentsInChildren<BlendshapeManager>())
                    {
                        blendshapeManager.setBlendshape("compress", ApparelData.CompressHair);
                    }
                }
            }

            //Set mask
            setTextureProperty(ApparelTables[slot].SkinMaskProperty, false, ApparelData.Mask);

            //Update apparel name in StoredCharacterData
            StoredCharacterData.ApparelNames[slot] = ApparelData.Name;
            StoredCharacterData.ApparelMaterials[slot] = materialSelection;
        }

        public void setApparelByName(string name, int slot, int materialSelection)
        {
            if (ApparelTables.Count <= slot) return;
            int index = ApparelTables[slot].Items.FindIndex(t => t.Name == name);
            if (index != -1) setApparel(index, slot, materialSelection);
        }

        public List<scrObj_Apparel.Apparel> getEquippedApparelData()
        {
            return ApparelTables.Zip(StoredCharacterData.ApparelNames, (table, name) => table.Items.FirstOrDefault(item => item.Name == name)).ToList();
        }

        public void setRandomOutfit()
        {
            if (Outfits.GetRandomOutfit(this, out var apparelOptions, out var apparelMaterials))
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
            if (name != "")
            {
                //Save property
                if (save) saveProperty(ref StoredCharacterData.Blendshapes, new CC_Property() { propertyName = name, floatValue = value });

                //Set body customization
                if (name.Contains("BodyCustomization")) { setBodyCustomization(name, value); return; }

                //Set blendshape on every mesh with a blendshape manager

                foreach (var manager in gameObject.GetComponentsInChildren<BlendshapeManager>())
                {
                    manager.setBlendshape(name, value);
                }
            }
        }

        public void setBodyCustomization(string name, float value)
        {
            var modifyBoneManager = GetComponentInChildren<ModifyBone_Manager>();
            if (modifyBoneManager != null) modifyBoneManager.setModifyValue(name, value);
        }

        public List<Material> getRelevantMaterials(int materialIndex, string meshTag)
        {
            IEnumerable<Renderer> meshes = string.IsNullOrEmpty(meshTag)
                ? gameObject.GetComponentsInChildren<Renderer>()
                : getMeshByTag(meshTag);

            //Convert to list of materials
            var materials = new List<Material>();
            foreach (var mesh in meshes)
            {
                if (materialIndex != -1)
                {
                    //Add single material at index if it exists
                    if (mesh.materials.Length > materialIndex)
                    {
                        materials.Add(mesh.materials[materialIndex]);
                    }
                }
                else
                {
                    //Add all materials
                    materials.AddRange(mesh.materials);
                }
            }

            return materials;
        }

        public List<Renderer> getMeshByTag(string tag)
        {
            return gameObject.GetComponentsInChildren<Renderer>().Where(m => m.gameObject.tag == tag).ToList();
        }

        //Set texture property
        public void setTextureProperty(CC_Property p, bool save = false, Texture2D t = null)
        {
            if (t != null) p.stringValue = t.name;
            //Get relevant materials and set texture
            foreach (var material in getRelevantMaterials(p.materialIndex, p.meshTag))
            {
                if (material.HasProperty(p.propertyName)) material.SetTexture(p.propertyName, (t != null) ? t : Resources.Load<Texture2D>(p.stringValue));
            }

            if (save) saveProperty(ref StoredCharacterData.TextureProperties, p);
        }

        //Set float property
        public void setFloatProperty(CC_Property p, bool save = false)
        {
            //Get relevant materials and set float
            foreach (var material in getRelevantMaterials(p.materialIndex, p.meshTag))
            {
                if (material.HasProperty(p.propertyName)) material.SetFloat(p.propertyName, p.floatValue);
            }

            if (save) saveProperty(ref StoredCharacterData.FloatProperties, p);
        }

        //Set color property
        public void setColorProperty(CC_Property p, bool save = false)
        {
            //Get relevant materials and set color
            foreach (var material in getRelevantMaterials(p.materialIndex, p.meshTag))
            {
                if (material.HasProperty(p.propertyName)) material.SetColor(p.propertyName, p.colorValue);
            }

            if (save) saveProperty(ref StoredCharacterData.ColorProperties, p);
        }

        public bool findProperty(List<CC_Property> properties, CC_Property p, out CC_Property pOut, out int index)
        {
            int i = properties.FindIndex(t => t.propertyName == p.propertyName && t.materialIndex == p.materialIndex && t.meshTag == p.meshTag);
            if (i >= 0)
            {
                pOut = properties[i];
                index = i;
                return true;
            }
            else
            {
                pOut = p;
                index = -1;
                return false;
            }
        }

        //Save property to list, overwrite if already exists
        public void saveProperty(ref List<CC_Property> properties, CC_Property p)
        {
            var propertyIndex = properties.FindIndex(t => t.materialIndex == p.materialIndex && t.propertyName == p.propertyName && t.meshTag == p.meshTag);

            if (propertyIndex == -1)
            {
                properties.Add(p);
            }
            else
            {
                properties[propertyIndex] = p;
            }
        }

        #endregion Customization

#if UNITY_EDITOR

        [CustomEditor(typeof(CharacterCustomization))]
        public class CharacterSelectorEditor : Editor
        {
            private SerializedProperty characterNameProp;

            private void OnEnable()
            {
                //Cache the serialized property for CharacterName
                characterNameProp = serializedObject.FindProperty("CharacterName");
            }

            public override void OnInspectorGUI()
            {
                CharacterCustomization characterSelector = (CharacterCustomization)target;

                serializedObject.Update();

                //Check if scrObj_Presets is assigned
                if (characterSelector.Presets != null && characterSelector.Presets.Presets.Count > 0)
                {
                    //Get the current selected index
                    string[] characterNames = characterSelector.Presets.Presets.Select(p => p.CharacterName).ToArray();
                    int oldIndex = ArrayUtility.IndexOf(characterNames, characterNameProp.stringValue);
                    int newIndex = EditorGUILayout.Popup(oldIndex, characterNames);

                    //Update the CharacterName when selection changes
                    if (newIndex != oldIndex && newIndex >= 0 && newIndex < characterNames.Length)
                    {
                        characterNameProp.stringValue = characterSelector.Presets.Presets[newIndex].CharacterName;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Please assign a Presets ScriptableObject.", MessageType.Warning);
                }

                //Apply any changes made to the serialized object
                serializedObject.ApplyModifiedProperties();

                //Optionally, draw the default inspector for other variables
                DrawDefaultInspector();
            }
        }

#endif
    }
}