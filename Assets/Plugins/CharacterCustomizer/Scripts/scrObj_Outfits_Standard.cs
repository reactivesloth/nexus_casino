using System.Collections;
using System.Linq;
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
            public int IndexToMatch;
        }

        public List<Outfit_Definition> Outfits;

        public override bool GetRandomOutfit(CharacterCustomization script, out List<string> apparelOptions, out List<int> apparelMaterials)
        {
            if (Outfits.Count < 1)
            {
                Debug.LogError("Tried to set random outfit but no outfits have been defined");
                apparelOptions = null;
                apparelMaterials = null;
                return false;
            }

            var outfit = Outfits[Random.Range(0, Outfits.Count)]; //Get random outfit definition (each outfit definition can have multiple options per slot)

            apparelOptions = new List<string>();
            apparelMaterials = new List<int>();

            //One Outfit_Options per apparel slot
            for (int i = 0; i < script.ApparelTables.Count; i++)
            {
                if (outfit.OutfitOptions.Count <= i)
                {
                    Debug.LogError("Outfit options not found");
                    return false;
                }

                //Get available options
                var options = outfit.OutfitOptions[i].Options;

                //Get random chance
                float rand = Random.Range(0f, 1f);

                //If no options available or if it rolls default, set default name
                if (options.Count <= 0 || rand < outfit.OutfitOptions[i].DefaultChance)
                {
                    apparelOptions.Add(outfit.OutfitOptions[i].DefaultName);
                    apparelMaterials.Add(0);
                    continue;
                }

                //Otherwise get random option
                GetRandomApparel(script.ApparelTables[i], options, out string apparelOption, out int apparelMaterial);

                apparelOptions.Add(apparelOption);
                apparelMaterials.Add(apparelMaterial);
            }

            //Match materials
            for (int i = 0; i < outfit.OutfitOptions.Count; i++)
            {
                if (outfit.OutfitOptions[i].MatchMaterials)
                {
                    apparelMaterials[i] = outfit.OutfitOptions[i].IndexToMatch;
                }
            }

            return true;
        }
    }
}