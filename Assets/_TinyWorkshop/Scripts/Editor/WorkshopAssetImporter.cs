using System.IO;
using UnityEditor;
using UnityEngine;
namespace TinyWorkshop.Editor
{
    public sealed class WorkshopAssetImporter : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_TinyWorkshop/Art/", MainArt = ArtRoot + "Main/";
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot) || !assetPath.EndsWith(".png"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (assetPath.StartsWith(MainArt))
            {
                // Main game redesign: painted high-res art (Tools/redesign_assets.py), 100 px == 1 unit.
                importer.spritePixelsPerUnit = 100;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                ApplyLayout(Path.GetFileNameWithoutExtension(assetPath), settings);
            }
            else
            {
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
            }
            // TextureImporterSettings carries PPU / filter too: copy them in, or SetTextureSettings reverts them.
            settings.spritePixelsPerUnit = importer.spritePixelsPerUnit;
            settings.filterMode = importer.filterMode;
            settings.wrapMode = importer.wrapMode;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.spriteMode = (int)SpriteImportMode.Single;
            importer.SetTextureSettings(settings);
        }
        // Pivots sit where objects touch the ground; borders make panels/bars 9-slice.
        static void ApplyLayout(string name, TextureImporterSettings s)
        {
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.spriteBorder = Vector4.zero;
            if (name.StartsWith("robot_") || name.StartsWith("machine_") || name.StartsWith("terminal_"))
                Pivot(s, new Vector2(.5f, 0));
            if (name == "machine_spark" || name == "bubble_heart" || name == "truck_bubble")
                Pivot(s, new Vector2(.5f, .5f));
            if (name == "platform")
                Pivot(s, new Vector2(.5f, 1));
            Vector4 border = name switch
            {
                "platform" => new Vector4(64, 0, 64, 0),
                "bottom_bar" => new Vector4(44, 40, 44, 40),
                "window_plain" => new Vector4(34, 34, 34, 34),
                "row" => new Vector4(22, 22, 22, 22),
                "bar_track" or "bar_fill" => new Vector4(13, 13, 13, 13),
                "keycap" => new Vector4(16, 16, 16, 18),
                "keycap_wide" => new Vector4(18, 16, 18, 18),
                "stage_label" => new Vector4(46, 40, 46, 40),
                "lv_chip" => new Vector4(12, 12, 12, 12),
                _ => Vector4.zero
            };
            s.spriteBorder = border;
        }
        static void Pivot(TextureImporterSettings s, Vector2 pivot)
        {
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
        }
    }
}
