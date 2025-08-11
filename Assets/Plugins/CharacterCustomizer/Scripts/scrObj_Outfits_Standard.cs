using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    [CreateAssetMenu(fileName = "Standard Outfit Collection", menuName = "ScriptableObjects/Outfits Standard")]
    public class scrObj_Outfits_Standard : scrObj_Outfits
    {
        [System.Serializable]
        public struct Outfit_Definition
        {
            public string OutfitName;
            public List<Outfit_Options> OutfitOptions;
        }

        [System.Serializable]
        public struct Outfit_Options
        {
            public string DefaultName;
            public List<string> Options;
            public float DefaultChance;
            public bool MatchMaterials;
            /// <summary>
            /// If MatchMaterials == true, copy the material index from this slot index.
            /// </summary>
            public int IndexToMatch;
        }

        public List<Outfit_Definition> Outfits = new List<Outfit_Definition>();

        public override bool GetRandomOutfit(CharacterCustomization script, out List<string> apparelOptions, out List<int> apparelMaterials)
        {
            apparelOptions = null;
            apparelMaterials = null;

            if (script == null)
            {
                Debug.LogError("GetRandomOutfit: script is null");
                return false;
            }

            if (Outfits == null || Outfits.Count == 0)
            {
                Debug.LogError("Tried to set random outfit but no outfits have been defined");
                return false;
            }

            var outfit = Outfits[Random.Range(0, Outfits.Count)];
            if (outfit.OutfitOptions == null || outfit.OutfitOptions.Count == 0)
            {
                Debug.LogError("Outfit options not found");
                return false;
            }

            if (script.ApparelTables == null || script.ApparelTables.Count == 0)
            {
                Debug.LogError("GetRandomOutfit: script.ApparelTables is empty");
                return false;
            }

            apparelOptions = new List<string>(script.ApparelTables.Count);
            apparelMaterials = new List<int>(script.ApparelTables.Count);

            // One Outfit_Options per apparel slot
            for (int i = 0; i < script.ApparelTables.Count; i++)
            {
                if (outfit.OutfitOptions.Count <= i)
                {
                    Debug.LogError("Outfit options not found for slot index: " + i);
                    apparelOptions.Clear();
                    apparelMaterials.Clear();
                    return false;
                }

                var opt = outfit.OutfitOptions[i];
                var options = opt.Options; // can be null

                // Random chance for default
                float rand = Random.Range(0f, 1f);

                // If no options available or if it rolls default, set default name
                if (options == null || options.Count == 0 || rand < opt.DefaultChance)
                {
                    apparelOptions.Add(opt.DefaultName ?? string.Empty);
                    apparelMaterials.Add(0);
                    continue;
                }

                // Otherwise get random option from table
                GetRandomApparel(script.ApparelTables[i], options, out string apparelOption, out int apparelMaterial);
                apparelOptions.Add(apparelOption);
                apparelMaterials.Add(apparelMaterial);
            }

            // Match materials: copy material index from IndexToMatch -> i
            for (int i = 0; i < outfit.OutfitOptions.Count && i < apparelMaterials.Count; i++)
            {
                var opt = outfit.OutfitOptions[i];
                if (!opt.MatchMaterials)
                    continue;

                int src = opt.IndexToMatch;
                if (src >= 0 && src < apparelMaterials.Count)
                {
                    apparelMaterials[i] = apparelMaterials[src];
                }
            }

            return true;
        }
    }
}