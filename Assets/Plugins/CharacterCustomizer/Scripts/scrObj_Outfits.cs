using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    public class scrObj_Outfits : ScriptableObject
    {
        // Переиспользуемый буфер, чтобы не аллоцировать каждый раз.
        private static readonly List<int> _filteredIndices = new List<int>(32);

        public virtual bool GetRandomOutfit(CharacterCustomization script, out List<string> apparelOptions, out List<int> apparelMaterials)
        {
            apparelOptions = null;
            apparelMaterials = null;
            return true;
        }

        public void GetRandomApparel(scrObj_Apparel apparelObj, List<string> choices, out string apparelOption, out int apparelMaterial)
        {
            apparelOption = "";
            apparelMaterial = 0;

            if (apparelObj == null || choices == null || choices.Count == 0)
            {
                return;
            }

            var items = apparelObj.Items;
            if (items == null || items.Count == 0)
            {
                return;
            }

            _filteredIndices.Clear();
            // Раньше тут был LINQ: Where(...).ToList()
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!string.IsNullOrEmpty(it.Name))
                {
                    // Contains на List<string> ок — без LINQ.
                    if (choices.Contains(it.Name))
                        _filteredIndices.Add(i);
                }
            }

            if (_filteredIndices.Count == 0)
            {
                return;
            }

            var chosenIndex = _filteredIndices[Random.Range(0, _filteredIndices.Count)];
            var randomChoice = items[chosenIndex];
            if (randomChoice.Materials == null || randomChoice.Materials.Count == 0)
            {
                // Имя заберём, но материалов нет — оставим 0
                apparelOption = randomChoice.Name;
                apparelMaterial = 0;
                return;
            }

            apparelOption = randomChoice.Name;
            apparelMaterial = Random.Range(0, randomChoice.Materials.Count);
        }
    }
}
