using UnityEditor;
using UnityEngine;

namespace Anathema.EditorTools
{
    // Import settings for the painted ability icons (Assets/Resources/AbilityIcons, rendered by Tools/IconRender):
    // uncompressed so the dark gradients don't band, with mipmaps so they stay smooth drawn smaller than 256px.
    public class AbilityIconImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/AbilityIcons/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
