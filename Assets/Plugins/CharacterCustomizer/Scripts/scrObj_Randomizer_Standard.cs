using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    [CreateAssetMenu(fileName = "Standard Randomizer", menuName = "ScriptableObjects/Randomizer Standard")]
    public class scrObj_Randomizer_Standard : scrObj_Randomizer
    {
        [Header("Hair Blacklists (by ethnicity)")]
        public List<string> hairBlacklistCaucasian;
        public List<string> hairBlacklistAfrican;
        public List<string> hairBlacklistAsian;

        // Reusable temp buffers to reduce GC spikes
        [System.NonSerialized] private List<string> _tmpNames;
        [System.NonSerialized] private List<string> _sanitized;
        [System.NonSerialized] private List<HairColor> _hairColorPool;
        [System.NonSerialized] private List<EyeColor> _eyeColorPool;

        private void EnsureBuffers()
        {
            if (_tmpNames == null) _tmpNames = new List<string>(16);
            if (_sanitized == null) _sanitized = new List<string>(32);
            if (_hairColorPool == null) _hairColorPool = new List<HairColor>(16);
            if (_eyeColorPool == null) _eyeColorPool = new List<EyeColor>(16);
        }

        public override IEnumerator randomizeAll(CharacterCustomization script)
        {
            EnsureBuffers();

            if (script == null)
                yield break;

            // Reset options
            if (script.LoadAsync && script.GetPresetData(script.CharacterName, out var ogPreset))
            {
                yield return script.ApplyCharacterVarsAsync(ogPreset);
            }
            else
            {
                script.LoadFromPreset(script.CharacterName);
            }

            if (script.LoadAsync) yield return null;

            var ethnicity = (Ethnicity)Random.Range(0, 3);
            var ageGroup = AgeGroup.Adult;

            // Hair per table
            if (script.HairTables != null)
            {
                for (int i = 0; i < script.HairTables.Count; i++)
                {
                    var table = script.HairTables[i];
                    _tmpNames.Clear();

                    if (table != null && table.Hairstyles != null)
                    {
                        for (int h = 0; h < table.Hairstyles.Count; h++)
                        {
                            var style = table.Hairstyles[h];
                            if (!string.IsNullOrEmpty(style.Name))
                                _tmpNames.Add(style.Name);
                        }
                    }

                    string hair = getRandomHair(_tmpNames, ethnicity);
                    script.setHairByName(hair, i);
                    if (script.LoadAsync) yield return null;
                }
            }

            // Hair color
            var hairColor = getRandomHairColor(ethnicity, ageGroup);
            script.setColorProperty(new CC_Property { propertyName = "_Hair_Tint", colorValue = hairColor }, true);

            // Eye color
            var eyeColor = getRandomEyeColor(ethnicity);
            script.setColorProperty(new CC_Property { propertyName = "_Eye_Color", colorValue = eyeColor, materialIndex = -1, meshTag = "Head" }, true);

            if (script.LoadAsync) yield return null;

            // Randomize mod shapes
            for (int i = 0; i < _modShapes.Length; i++)
            {
                float val = GenerateNormalRandom(0.2f);
                script.setBlendshapeByName(_modShapes[i], val);
            }

            // Freckles
            float frecklesRand = Mathf.Abs(GenerateNormalRandom(0.5f));
            script.setFloatProperty(new CC_Property { propertyName = "_Freckles_Strength", floatValue = frecklesRand, materialIndex = 0, meshTag = "Head" }, true);

            // Skin tint
            Color skinColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            float skinRand = Mathf.Abs(GenerateNormalRandom(0.1f));
            skinColor.a = skinRand;
            script.setColorProperty(new CC_Property { propertyName = "_Skin_Tint", colorValue = skinColor, materialIndex = 0 }, true);

            // Lips
            Color lipsColor = new Color(0.8f, 0.2f, 0.2f);
            float lipsRand = Mathf.Abs(GenerateNormalRandom(0.1f));
            lipsColor.a = lipsRand;
            script.setColorProperty(new CC_Property { propertyName = "_Lips_Color", colorValue = lipsColor, materialIndex = 0, meshTag = "Head" }, true);

            if (script.LoadAsync) yield return null;

            // Face shapes reset
            for (int i = 0; i < _faceShapes.Length; i++)
                script.setBlendshapeByName(_faceShapes[i], 0);

            // Build a working list
            _faceWork.Clear();
            _faceWork.AddRange(_faceShapes);

            // Pick secondary first from full list
            string secondaryShape = _faceWork[Random.Range(0, _faceWork.Count)];
            _faceWork.Remove(secondaryShape);

            // Remove ethnicity-specific entries
            ApplyEthnicityFaceFilters(ethnicity, _faceWork);

            // Choose main shape
            string mainShape = _faceWork.Count > 0 ? _faceWork[Random.Range(0, _faceWork.Count)] : secondaryShape;
            float secondaryRand = Mathf.Abs(GenerateNormalRandom(0.33f));
            script.setBlendshapeByName(secondaryShape, secondaryRand);
            script.setBlendshapeByName(mainShape, 1f - secondaryRand);

            if (script.LoadAsync) yield return null;

            // Skin textures
            int selectedTexture = 0;
            float rand = Random.Range(0f, 1f);
            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    selectedTexture = 0;
                    break;
                case Ethnicity.African:
                    selectedTexture = (rand > 0.5f) ? 1 : 2;
                    break;
                case Ethnicity.Other:
                    selectedTexture = (rand > 0.5f) ? 0 : 2;
                    break;
                case Ethnicity.Asian:
                    selectedTexture = (rand > 0.25f) ? 0 : 2;
                    break;
            }

            script.setTextureProperty(new CC_Property { propertyName = "_Color_Map", stringValue = _headTextures[selectedTexture], meshTag = "Head", materialIndex = 0 }, true);
            script.setTextureProperty(new CC_Property { propertyName = "_Color_Map", stringValue = _bodyTextures[selectedTexture], meshTag = "Body", materialIndex = 0 }, true);

            // Height & weight
            script.setBlendshapeByName("BodyCustomization_Height", GenerateNormalRandom(0.33f), true);
            script.setBlendshapeByName("BodyCustomization_Weight", GenerateNormalRandom(0.33f));

            if (script.LoadAsync) yield return null;

            // Default outfit
            script.setApparelByName("UpperBody_Default", 0, 0);
            if (script.LoadAsync) yield return null;
            script.setApparelByName("LowerBody_Default", 1, 0);
            if (script.LoadAsync) yield return null;
            script.setApparelByName("Footwear_Default", 2, 0);
            if (script.LoadAsync) yield return null;
            script.setApparelByName("Headwear_Default", 3, 0);
        }

        private enum Ethnicity { Caucasian, African, Asian, Other }
        private enum AgeGroup { Young, Adult, Elderly }
        private enum EyeColor { LightBrown, MediumBrown, DarkBrown, Amber, Hazel, Green, LightBlue, DarkBlue }
        private enum HairColor { LightBrown, MediumBrown, DarkBrown, Blonde, Black, LightGray, DarkGray }

        private static readonly string[] _modShapes = new string[]
        {
            "mod_brow_height","mod_brow_depth","mod_jaw_height","mod_jaw_width","mod_cheeks_size","mod_cheekbone_size",
            "mod_nose_height","mod_nose_width","mod_nose_out","mod_nose_size","mod_mouth_size","mod_mouth_depth",
            "mod_mouth_height","mod_eyes_depth","mod_eyes_height","mod_eyes_narrow","mod_chin_size"
        };

        private static readonly string[] _faceShapes = new string[]
        {
            "", "shp_head_01", "shp_head_02", "shp_head_03", "shp_head_04", "shp_head_05", "shp_head_06", "shp_head_07", "shp_head_08"
        };

        private static readonly string[] _headTextures = new string[] { "T_Skin_Head_01", "T_Skin_Head_02", "T_Skin_Head_03" };
        private static readonly string[] _bodyTextures = new string[] { "T_Skin_Body_01", "T_Skin_Body_02", "T_Skin_Body_03" };

        [System.NonSerialized] private readonly List<string> _faceWork = new List<string>(16);

        private void ApplyEthnicityFaceFilters(Ethnicity ethnicity, List<string> work)
        {
            // Remove entries according to original logic
            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    work.Remove("shp_head_01");
                    work.Remove("shp_head_04");
                    work.Remove("shp_head_06");
                    work.Remove("shp_head_07");
                    break;
                case Ethnicity.African:
                    if (work.Count > 0) work.Remove("");
                    work.Remove("shp_head_02");
                    work.Remove("shp_head_03");
                    work.Remove("shp_head_04");
                    work.Remove("shp_head_05");
                    work.Remove("shp_head_06");
                    work.Remove("shp_head_08");
                    break;
                case Ethnicity.Asian:
                    if (work.Count > 0) work.Remove("");
                    work.Remove("shp_head_01");
                    work.Remove("shp_head_02");
                    work.Remove("shp_head_03");
                    work.Remove("shp_head_05");
                    work.Remove("shp_head_07");
                    work.Remove("shp_head_08");
                    break;
            }
        }

        private Color getRandomHairColor(Ethnicity ethnicity, AgeGroup ageGroup)
        {
            EnsureBuffers();
            _hairColorPool.Clear();

            // Seed pool by ethnicity with duplicates to bias probability
            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    _hairColorPool.Add(HairColor.LightBrown);
                    _hairColorPool.Add(HairColor.MediumBrown);
                    _hairColorPool.Add(HairColor.MediumBrown);
                    _hairColorPool.Add(HairColor.DarkBrown);
                    _hairColorPool.Add(HairColor.DarkBrown);
                    _hairColorPool.Add(HairColor.Black);
                    _hairColorPool.Add(HairColor.Blonde);
                    _hairColorPool.Add(HairColor.LightGray);
                    _hairColorPool.Add(HairColor.DarkGray);
                    break;
                case Ethnicity.Asian:
                    _hairColorPool.Add(HairColor.DarkBrown);
                    _hairColorPool.Add(HairColor.DarkBrown);
                    _hairColorPool.Add(HairColor.Black);
                    _hairColorPool.Add(HairColor.Black);
                    _hairColorPool.Add(HairColor.LightGray);
                    _hairColorPool.Add(HairColor.DarkGray);
                    break;
                case Ethnicity.African:
                case Ethnicity.Other:
                    _hairColorPool.Add(HairColor.DarkBrown);
                    _hairColorPool.Add(HairColor.DarkBrown);
                    _hairColorPool.Add(HairColor.DarkBrown);
                    _hairColorPool.Add(HairColor.Black);
                    _hairColorPool.Add(HairColor.Black);
                    _hairColorPool.Add(HairColor.LightGray);
                    _hairColorPool.Add(HairColor.DarkGray);
                    break;
            }

            // Age filters (no LINQ Except)
            if (ageGroup == AgeGroup.Elderly)
            {
                RemoveIfPresent(_hairColorPool, HairColor.LightBrown);
                RemoveIfPresent(_hairColorPool, HairColor.MediumBrown);
                RemoveIfPresent(_hairColorPool, HairColor.Blonde);
            }
            else
            {
                RemoveIfPresent(_hairColorPool, HairColor.LightGray);
                RemoveIfPresent(_hairColorPool, HairColor.DarkGray);
            }

            if (_hairColorPool.Count == 0)
                return getHairColor(HairColor.DarkBrown);

            return getHairColor(_hairColorPool[Random.Range(0, _hairColorPool.Count)]);
        }

        private static void RemoveIfPresent<T>(List<T> list, T value)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (EqualityComparer<T>.Default.Equals(list[i], value))
                    list.RemoveAt(i);
            }
        }

        private string getRandomHair(List<string> options, Ethnicity ethnicity)
        {
            EnsureBuffers();
            _sanitized.Clear();

            if (options == null || options.Count == 0)
                return string.Empty;

            List<string> blacklist = null;
            switch (ethnicity)
            {
                case Ethnicity.Caucasian: blacklist = hairBlacklistCaucasian; break;
                case Ethnicity.African:   blacklist = hairBlacklistAfrican;   break;
                case Ethnicity.Asian:     blacklist = hairBlacklistAsian;     break;
            }

            if (blacklist == null || blacklist.Count == 0)
            {
                // No blacklist — use all
                for (int i = 0; i < options.Count; i++)
                {
                    var s = options[i];
                    if (!string.IsNullOrEmpty(s))
                        _sanitized.Add(s);
                }
            }
            else
            {
                for (int i = 0; i < options.Count; i++)
                {
                    var s = options[i];
                    if (!string.IsNullOrEmpty(s) && !blacklist.Contains(s))
                        _sanitized.Add(s);
                }
            }

            if (_sanitized.Count == 0)
                return options[0]; // fallback to first available

            return _sanitized[Random.Range(0, _sanitized.Count)];
        }

        private Color getRandomEyeColor(Ethnicity ethnicity)
        {
            EnsureBuffers();
            _eyeColorPool.Clear();

            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    _eyeColorPool.Add(EyeColor.LightBrown);
                    _eyeColorPool.Add(EyeColor.MediumBrown);
                    _eyeColorPool.Add(EyeColor.Amber);
                    _eyeColorPool.Add(EyeColor.Hazel);
                    _eyeColorPool.Add(EyeColor.Green);
                    _eyeColorPool.Add(EyeColor.LightBlue);
                    _eyeColorPool.Add(EyeColor.DarkBlue);
                    break;
                case Ethnicity.Asian:
                    _eyeColorPool.Add(EyeColor.DarkBrown);
                    _eyeColorPool.Add(EyeColor.MediumBrown);
                    break;
                case Ethnicity.African:
                case Ethnicity.Other:
                    _eyeColorPool.Add(EyeColor.DarkBrown);
                    _eyeColorPool.Add(EyeColor.MediumBrown);
                    _eyeColorPool.Add(EyeColor.Amber);
                    _eyeColorPool.Add(EyeColor.Hazel);
                    break;
            }

            if (_eyeColorPool.Count == 0)
                return getEyeColor(EyeColor.MediumBrown);

            return getEyeColor(_eyeColorPool[Random.Range(0, _eyeColorPool.Count)]);
        }

        private static Color getEyeColor(EyeColor eyeColor)
        {
            Color color;
            switch (eyeColor)
            {
                case EyeColor.LightBrown: ColorUtility.TryParseHtmlString("#875E40", out color); break;
                case EyeColor.MediumBrown: ColorUtility.TryParseHtmlString("#604531", out color); break;
                case EyeColor.DarkBrown: ColorUtility.TryParseHtmlString("#3A2B1F", out color); break;
                case EyeColor.Amber: ColorUtility.TryParseHtmlString("#87763C", out color); break;
                case EyeColor.Hazel: ColorUtility.TryParseHtmlString("#9C9662", out color); break;
                case EyeColor.Green: ColorUtility.TryParseHtmlString("#677851", out color); break;
                case EyeColor.LightBlue: ColorUtility.TryParseHtmlString("#698AA3", out color); break;
                case EyeColor.DarkBlue: ColorUtility.TryParseHtmlString("#4E6373", out color); break;
                default: return Color.black;
            }
            return color;
        }

        private static Color getHairColor(HairColor hairColor)
        {
            Color color;
            switch (hairColor)
            {
                case HairColor.LightBrown: ColorUtility.TryParseHtmlString("#947259", out color); break;
                case HairColor.MediumBrown: ColorUtility.TryParseHtmlString("#604938", out color); break;
                case HairColor.DarkBrown: ColorUtility.TryParseHtmlString("#3A2D22", out color); break;
                case HairColor.Blonde: ColorUtility.TryParseHtmlString("#A18D64", out color); break;
                case HairColor.LightGray: ColorUtility.TryParseHtmlString("#A1A1A1", out color); break;
                case HairColor.DarkGray: ColorUtility.TryParseHtmlString("#4D4D4D", out color); break;
                case HairColor.Black: ColorUtility.TryParseHtmlString("#1A1A1A", out color); break;
                default: return Color.black;
            }
            return color;
        }
    }
}
