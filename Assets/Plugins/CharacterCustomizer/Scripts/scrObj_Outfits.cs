using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    public class scrObj_Outfits : ScriptableObject
    {
        public virtual bool GetRandomOutfit(CharacterCustomization script, out List<string> apparelOptions, out List<int> apparelMaterials)
        {
            apparelOptions = null;
            apparelMaterials = null;
            return true;
        }

        public void GetRandomApparel(scrObj_Apparel apparelObj, List<string> choices, out string apparelOption, out int apparelMaterial)
        {
            if (apparelObj == null)
            {
                apparelOption = "";
                apparelMaterial = 0;
                return;
            }

            var filteredApparel = apparelObj.Items.Where(item => choices.Contains(item.Name)).ToList();

            if (filteredApparel.Count < 1)
            {
                apparelOption = "";
                apparelMaterial = 0;
                return;
            }

            var randomChoice = filteredApparel[Random.Range(0, filteredApparel.Count)];

            apparelOption = randomChoice.Name;
            apparelMaterial = Random.Range(0, randomChoice.Materials.Count);
            return;
        }
    }
}