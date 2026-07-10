#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace KoG.MiniMvp.EditorTools
{
    /// <summary>
    /// One-click: set Environment / Nature textures to ASTC (Android) + compressed (iOS).
    /// Menu: KoG / Apply Mobile Texture Compression
    /// </summary>
    public static class MobileTextureCompression
    {
        const string EnvRoot = "Assets/Resources/Environment";
        const string MatRoot = "Assets/Materials";
        const string ArtRoot = "Assets/Art";

        [MenuItem("KoG/Apply Mobile Texture Compression")]
        public static void Apply()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { EnvRoot, MatRoot, ArtRoot });
            var n = 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.maxTextureSize = Mathf.Min(android.maxTextureSize > 0 ? android.maxTextureSize : 2048, 2048);
                importer.SetPlatformTextureSettings(android);

                var ios = importer.GetPlatformTextureSettings("iPhone");
                ios.overridden = true;
                ios.format = TextureImporterFormat.ASTC_6x6;
                ios.maxTextureSize = Mathf.Min(ios.maxTextureSize > 0 ? ios.maxTextureSize : 2048, 2048);
                importer.SetPlatformTextureSettings(ios);

                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                n++;
            }

            Debug.Log("[KoG] Mobile texture compression applied to " + n + " textures (ASTC_6x6).");
        }
    }
}
#endif
