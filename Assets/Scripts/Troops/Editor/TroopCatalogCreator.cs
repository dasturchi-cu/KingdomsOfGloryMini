#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KoG.MiniMvp.Troops.Editor
{
    /// <summary>One-click create default barbarian definition + catalog under Resources/Troops.</summary>
    public static class TroopCatalogCreator
    {
        const string Folder = "Assets/Resources/Troops";

        [MenuItem("KoG/Troops/Create Default Catalog")]
        public static void CreateDefaultCatalog()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Resources", "Troops");

            var defPath = Folder + "/Troop_Barbarian.asset";
            var def = AssetDatabase.LoadAssetAtPath<TroopDefinition>(defPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<TroopDefinition>();
                def.Id = "barbarian";
                def.DisplayName = "Barbarian";
                def.Stats = TroopStats.BarbarianDefault;
                def.PoolPrewarm = 10;
                def.PoolMax = 40;
                def.CastShadows = false;
                AssetDatabase.CreateAsset(def, defPath);
            }

            var catPath = Folder + "/TroopCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<TroopCatalogAsset>(catPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TroopCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, catPath);
            }

            catalog.Definitions = new[] { def };
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;
            Debug.Log("[KoG] Created " + catPath);
        }
    }
}
#endif
