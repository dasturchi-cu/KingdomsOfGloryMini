#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KoG.MiniMvp.EditorTools
{
    /// <summary>
    /// PolygonApocalypse lives in Assets/PolygonApocalypse~ (tilde = Unity ignores import).
    /// Menu restores or re-excludes if someone renames the folder back into Assets.
    /// </summary>
    public static class ExcludeThirdPartyPacks
    {
        const string ActiveFolder = "Assets/PolygonApocalypse";
        const string ExcludedFolder = "Assets/PolygonApocalypse~";

        [MenuItem("KoG/Third-Party/Exclude PolygonApocalypse From Project")]
        public static void ExcludePolygon()
        {
            if (AssetDatabase.IsValidFolder(ExcludedFolder) || System.IO.Directory.Exists(ExcludedFolder))
            {
                Debug.Log("[KoG] PolygonApocalypse already excluded (PolygonApocalypse~).");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ActiveFolder) && !System.IO.Directory.Exists(ActiveFolder))
            {
                Debug.LogWarning("[KoG] PolygonApocalypse folder not found.");
                return;
            }

            var err = AssetDatabase.MoveAsset(ActiveFolder, ExcludedFolder);
            if (!string.IsNullOrEmpty(err))
            {
                // Tilde folders are often outside AssetDatabase — fall back to filesystem rename.
                try
                {
                    System.IO.Directory.Move(
                        System.IO.Path.GetFullPath(ActiveFolder),
                        System.IO.Path.GetFullPath(ExcludedFolder));
                    var meta = ActiveFolder + ".meta";
                    var metaOut = ExcludedFolder + ".meta";
                    if (System.IO.File.Exists(meta))
                        System.IO.File.Move(meta, metaOut);
                    AssetDatabase.Refresh();
                    Debug.Log("[KoG] PolygonApocalypse excluded via filesystem → PolygonApocalypse~");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[KoG] Exclude failed: " + err + " / " + e.Message);
                }
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log("[KoG] PolygonApocalypse excluded from import/build (PolygonApocalypse~).");
        }

        [MenuItem("KoG/Third-Party/Restore PolygonApocalypse Into Project")]
        public static void RestorePolygon()
        {
            if (!System.IO.Directory.Exists(ExcludedFolder))
            {
                Debug.LogWarning("[KoG] Excluded folder PolygonApocalypse~ not found.");
                return;
            }

            try
            {
                System.IO.Directory.Move(
                    System.IO.Path.GetFullPath(ExcludedFolder),
                    System.IO.Path.GetFullPath(ActiveFolder));
                var meta = ExcludedFolder + ".meta";
                var metaOut = ActiveFolder + ".meta";
                if (System.IO.File.Exists(meta))
                    System.IO.File.Move(meta, metaOut);
                AssetDatabase.Refresh();
                Debug.Log("[KoG] PolygonApocalypse restored to Assets/PolygonApocalypse");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[KoG] Restore failed: " + e.Message);
            }
        }
    }
}
#endif
