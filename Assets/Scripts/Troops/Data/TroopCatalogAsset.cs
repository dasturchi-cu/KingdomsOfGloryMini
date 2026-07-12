using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>Root catalog of troop definitions. Place one asset under Resources/Troops/TroopCatalog.</summary>
    [CreateAssetMenu(fileName = "TroopCatalog", menuName = "KoG/Troops/Troop Catalog", order = 11)]
    public sealed class TroopCatalogAsset : ScriptableObject
    {
        public TroopDefinition[] Definitions;

        public bool TryGet(string id, out TroopDefinition def)
        {
            def = null;
            if (Definitions == null || string.IsNullOrEmpty(id)) return false;
            for (var i = 0; i < Definitions.Length; i++)
            {
                var d = Definitions[i];
                if (d != null && d.Id == id)
                {
                    def = d;
                    return true;
                }
            }

            return false;
        }
    }
}
