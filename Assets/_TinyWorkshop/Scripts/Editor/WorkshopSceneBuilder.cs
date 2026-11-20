using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TinyWorkshop.Editor
{
    public static partial class WorkshopSceneBuilder
    {
        const string Root = "Assets/_TinyWorkshop/";
        static readonly string[] SceneNames = { "01_OneButton", "02_Movement", "03_Physics", "04_MainGame" };
        static Material spriteMaterial;
        static PhysicsMaterial2D gearPhysics, robotPhysics, bouncePhysics;
        static MovementProfile[] movement;
        static ProductionProfile[] production;
        static FeedbackProfile feedback;
        static Font font;
        [MenuItem("Tiny Workshop/Build or Rebuild All Scenes")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before rebuilding scenes.");
            foreach (var folder in new[] { "Scenes", "Prefabs", "Settings", "Materials" })
                Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(Root + "Art", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/PixelSprite.mat");
            if (!spriteMaterial)
            {
                spriteMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(spriteMaterial, Root + "Materials/PixelSprite.mat");
            }
            gearPhysics = PhysicsAsset("Gear", .1f, .35f);
            robotPhysics = PhysicsAsset("Robot", 0, 0);
            bouncePhysics = PhysicsAsset("Bumper", 0, .85f);
            CreateSettings();
            ConfigureLayers();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateMainGameAssets();
            for (int i = 0; i < 4; i++)
                BuildScene((WorkshopMode)i);
            EditorBuildSettings.scenes = SceneNames.Select(name => new EditorBuildSettingsScene(Root + "Scenes/" + name + ".unity", true)).ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Root + "Scenes/04_MainGame.unity");
            Debug.Log("Tiny Workshop: built four standalone scenes on the painted art, shared prefabs and profiles.");
        }
        [MenuItem("Tiny Workshop/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            Directory.CreateDirectory("Builds/TinyWorkshop");
            PlayerSettings.productName = "Tiny Workshop";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            var scenes = new[] { Root + "Scenes/04_MainGame.unity" }.Concat(SceneNames.Take(3).Select(n => Root + "Scenes/" + n + ".unity")).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, "Builds/TinyWorkshop/TinyWorkshop.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("Tiny Workshop Windows build succeeded: " + report.summary.totalSize + " bytes");
        }
        static void ConfigureLayers()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            string[] names = { "WorkshopGround", "WorkshopGear", "WorkshopRobot", "WorkshopPickup" };
            for (int i = 0; i < names.Length; i++)
                layers.GetArrayElementAtIndex(i + 8).stringValue = names[i];
            tags.ApplyModifiedProperties();
            Physics2D.IgnoreLayerCollision(9, 10, true);
            for (int i = 0; i < 32; i++)
                Physics2D.IgnoreLayerCollision(11, i, i != 10);
        }
        static T Asset<T>(string name) where T : ScriptableObject
        {
            string path = Root + "Settings/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<T>();
                asset.name = name;
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
        static PhysicsMaterial2D PhysicsAsset(string name, float friction, float bounce)
        {
            string path = Root + "Materials/" + name + ".physicsMaterial2D";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (!mat)
            {
                mat = new PhysicsMaterial2D(name);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.friction = friction;
            mat.bounciness = bounce;
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static void CreateSettings()
        {
            movement = new[] { Asset<MovementProfile>("Movement_Normal"), Asset<MovementProfile>("Movement_Heavy"), Asset<MovementProfile>("Movement_Light") };
            movement[1].maxSpeed = 4.5f;
            movement[1].acceleration = 24;
            movement[1].braking = 30;
            movement[1].jumpSpeed = 10;
            movement[1].coyoteTime = .07f;
            movement[1].jumpBuffer = .08f;
            movement[1].jumpCut = .65f;
            movement[2].maxSpeed = 6.2f;
            movement[2].acceleration = 60;
            movement[2].braking = 80;
            movement[2].jumpSpeed = 11;
            movement[2].coyoteTime = .14f;
            movement[2].jumpBuffer = .14f;
            movement[2].jumpCut = .45f;
            production = new[] { Asset<ProductionProfile>("Production_Normal"), Asset<ProductionProfile>("Production_Calm"), Asset<ProductionProfile>("Production_Hurried") };
            production[1].pressDuration = .15f;
            production[1].cooldown = .19f;
            production[2].pressDuration = .08f;
            production[2].cooldown = .08f;
            feedback = Asset<FeedbackProfile>("Feedback_Default");
            feedback.shakeStrength = .15f;
            foreach (var asset in movement.Cast<UnityEngine.Object>().Concat(production).Append(feedback))
                EditorUtility.SetDirty(asset);
        }
        static GameObject Ground(string name, Vector2 position, Vector2 size, bool visual = true, Transform parent = null)
        {
            var go = new GameObject(name);
            go.layer = 8;
            if (parent)
                go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size;
            if (visual)
            {
                // Painted plank: pivot on its top edge, 9-sliced horizontally to the collider width.
                var sr = MainSpriteRenderer("Platform", "platform", new Vector2(0, size.y * .5f + .02f), 1, go.transform);
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(size.x + .06f, layout.platformSize.y);
            }
            return go;
        }
        static void BuildScene(WorkshopMode mode)
        {
            if (mode == WorkshopMode.MainGame)
                BuildMainGameScene();
            else
                BuildLabScene(mode);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }
        static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        static Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Rect(name, parent, pos, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        static void BuildHud(WorkshopSession session)
        {
            var canvas = new GameObject("Workshop HUD").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var events = new GameObject("EventSystem").AddComponent<EventSystem>();
            events.gameObject.AddComponent<InputSystemUIInputModule>();
            var hud = canvas.gameObject.AddComponent<WorkshopHud>();
            hud.session = session;
            hud.hintFollowsWorld = session.mode == WorkshopMode.MainGame;
            session.Hud = hud;
            if (session.mode == WorkshopMode.MainGame)
                BuildMainHud(session, canvas, hud);
            else
                BuildLabHud(session, canvas, hud);
        }
    }
}
