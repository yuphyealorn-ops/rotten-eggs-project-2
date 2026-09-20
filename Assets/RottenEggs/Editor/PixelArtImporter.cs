using UnityEditor;
using UnityEngine;

namespace RottenEggs.EditorTools
{
    /// <summary>
    /// Gives every texture under Resources/Sprites the settings pixel art needs
    /// the moment it is imported: readable (the game reads the pixels into its
    /// canvas), point-filtered, uncompressed, no mipmaps. Without this, a fresh
    /// PNG comes in blurred and compressed and the loaders refuse it.
    /// </summary>
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Sprites/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }

        /// <summary>Re-runs the import for art that was already in the folder before this existed.</summary>
        [MenuItem("Rotten Eggs/Reimport Pixel Art")]
        public static void ReimportPixelArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') });
            foreach (string guid in guids)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }

            Debug.Log("Rotten Eggs: re-imported " + guids.Length + " textures under " + Folder + " as pixel art.");
        }
    }
}
