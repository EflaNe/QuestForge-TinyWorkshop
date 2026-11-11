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
        static readonly Color Ink = new Color(.075f, .13f, .16f, .96f), Cream = new Color(.93f, .9f, .76f), Mint = new Color(.65f, .88f, .7f), Orange = new Color(.96f, .69f, .34f);
        static Material spriteMaterial;
        static PhysicsMaterial2D gearPhysics, robotPhysics, bouncePhysics;
        static MovementProfile[] movement;
        static ProductionProfile[] production;
        static FeedbackProfile feedback;
        static GameObject robotPrefab, machinePrefab;
        static Gear gearPrefab;
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
            CreatePrefabs();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateMainGameAssets();
            for (int i = 0; i < 4; i++)
                BuildScene((WorkshopMode)i);
            EditorBuildSettings.scenes = SceneNames.Select(name => new EditorBuildSettingsScene(Root + "Scenes/" + name + ".unity", true)).ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Root + "Scenes/04_MainGame.unity");
            Debug.Log("Tiny Workshop: built four standalone scenes, shared prefabs and profiles.");
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
        static SpriteRenderer Sprite(string name, string asset, Vector2 position, int order = 0, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent)
                go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/" + asset + ".png");
            sr.sharedMaterial = spriteMaterial;
            sr.sortingOrder = order;
            return sr;
        }
        static SpriteRenderer Block(string name, Vector2 position, Vector2 size, Color color, int order = 0, Transform parent = null)
        {
            var sr = Sprite(name, "white", position, order, parent);
            sr.transform.localScale = new Vector3(size.x * 16, size.y * 16, 1);
            sr.color = color;
            return sr;
        }
        static void CreatePrefabs()
        {
            var robot = new GameObject("Robot");
            robot.layer = 10;
            var body = robot.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.gravityScale = 3.5f;
            var collider = robot.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(.56f, .88f);
            collider.sharedMaterial = robotPhysics;
            var controller = robot.AddComponent<RobotController2D>();
            controller.visual = Sprite("Visual", "robot", Vector2.zero, 10, robot.transform).transform;
            controller.profiles = movement;
            controller.carrySprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/gear.png");
            robotPrefab = PrefabUtility.SaveAsPrefabAsset(robot, Root + "Prefabs/Robot.prefab");
            UnityEngine.Object.DestroyImmediate(robot);
            var gear = new GameObject("Gear");
            gear.layer = 9;
            var gearBody = gear.AddComponent<Rigidbody2D>();
            gearBody.gravityScale = 2.2f;
            gearBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            gearBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            var circle = gear.AddComponent<CircleCollider2D>();
            circle.radius = .25f;
            circle.sharedMaterial = gearPhysics;
            var component = gear.AddComponent<Gear>();
            Sprite("Visual", "gear", Vector2.zero, 8, gear.transform);
            var pickup = new GameObject("PickupTrigger");
            pickup.layer = 11;
            pickup.transform.SetParent(gear.transform, false);
            pickup.AddComponent<CircleCollider2D>().isTrigger = true;
            pickup.GetComponent<CircleCollider2D>().radius = .44f;
            pickup.AddComponent<GearPickup>().gear = component;
            gearPrefab = PrefabUtility.SaveAsPrefabAsset(gear, Root + "Prefabs/Gear.prefab").GetComponent<Gear>();
            UnityEngine.Object.DestroyImmediate(gear);
            var machine = new GameObject("WorkshopMachine");
            var comp = machine.AddComponent<WorkshopMachine>();
            comp.profiles = production;
            comp.bodyVisual = Sprite("Body", "machine", Vector2.zero, 3, machine.transform).transform;
            comp.buttonVisual = Sprite("OrangeButton", "button", new Vector2(-.12f, -.13f), 4, machine.transform).transform;
            var click = machine.AddComponent<BoxCollider2D>();
            click.size = new Vector2(2.9f, 3);
            click.isTrigger = true;
            comp.clickArea = click;
            comp.outlet = new GameObject("GearOutlet").transform;
            comp.outlet.SetParent(machine.transform, false);
            comp.outlet.localPosition = new Vector3(1.35f, .05f, 0);
            comp.chargeLights = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
                comp.chargeLights[i] = Block("Energy_" + i, new Vector2(-.35f + i * .21f, .53f), new Vector2(.14f, .1f), Mint, 5, machine.transform);
            machinePrefab = PrefabUtility.SaveAsPrefabAsset(machine, Root + "Prefabs/WorkshopMachine.prefab");
            UnityEngine.Object.DestroyImmediate(machine);
        }
        static void Ground(string name, Vector2 position, Vector2 size, bool visual = true)
        {
            var go = new GameObject(name);
            go.layer = 8;
            go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size;
            if (visual)
            {
                var sr = Sprite("Platform", "platform", Vector2.zero, 1, go.transform);
                sr.transform.localScale = new Vector3(size.x / 3, size.y / .5f, 1);
            }
        }
        static void BuildScene(WorkshopMode mode)
        {
            if (mode == WorkshopMode.MainGame)
            {
                BuildMainGameScene();
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var session = new GameObject("WorkshopSession").AddComponent<WorkshopSession>();
            session.mode = mode;
            session.gearPrefab = gearPrefab;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = Ink;
            camera.clearFlags = CameraClearFlags.SolidColor;
            var pixel = camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 32;
            pixel.refResolutionX = 640;
            pixel.refResolutionY = 360;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixel.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            camera.gameObject.AddComponent<AudioListener>();
            session.worldCamera = camera;
            new GameObject("Workshop Light").AddComponent<Light>().type = LightType.Directional;
            Sprite("WorkshopBackdrop", "workshop", Vector2.zero, -20);
            session.Feedback = new GameObject("Feedback").AddComponent<WorkshopFeedback>();
            session.Feedback.profile = feedback;
            session.Feedback.worldCamera = camera;
            session.Feedback.sparkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/spark.png");
            session.Feedback.clips = Directory.GetFiles(Root + "Audio", "*.wav").Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).Where(c => c.name != "music" && c.name != "ambience").ToArray();
            session.Feedback.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/music.wav");
            session.Feedback.ambienceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/ambience.wav");
            session.lamps = new SpriteRenderer[3];
            session.lampGlows = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                session.lamps[i] = Sprite("CeilingLamp_" + i, "lamp", new Vector2(-6.5f + i * 6.5f, 3.15f), -5);
                session.lamps[i].color = new Color(.65f, .7f, .6f);
                session.lampGlows[i] = Sprite("LampGlow_" + i, "glow", new Vector2(-6.5f + i * 6.5f, 1.45f), -4);
                session.lampGlows[i].transform.localScale = Vector3.one * 3;
                session.lampGlows[i].color = new Color(1, .78f, .4f, .045f);
            }
            Ground("LeftWall", new Vector2(-9.8f, 0), new Vector2(.3f, 20), false);
            Ground("RightWall", new Vector2(9.8f, 0), new Vector2(.3f, 20), false);
            if (mode != WorkshopMode.Movement)
                Ground("Floor", new Vector2(0, -4.65f), new Vector2(20, 1.7f), false);
            if (mode == WorkshopMode.MainGame || mode == WorkshopMode.Movement)
            {
                var robot = (GameObject)PrefabUtility.InstantiatePrefab(robotPrefab);
                robot.transform.position = new Vector3(-8, -3.3f);
                session.Robot = robot.GetComponent<RobotController2D>();
                session.Robot.session = session;
            }
            if (mode == WorkshopMode.MainGame || mode == WorkshopMode.OneButton)
            {
                var machine = (GameObject)PrefabUtility.InstantiatePrefab(machinePrefab);
                machine.transform.position = mode == WorkshopMode.MainGame ? new Vector3(-6.4f, -2.3f) : new Vector3(-3, -2.3f);
                session.Machine = machine.GetComponent<WorkshopMachine>();
                session.Machine.session = session;
            }
            if (mode == WorkshopMode.MainGame)
            {
                Ground("LowPlatform", new Vector2(-1.65f, -2.9f), new Vector2(2.7f, .45f));
                Ground("MiddlePlatform", new Vector2(.9f, -1.8f), new Vector2(2.4f, .45f));
                Ground("RightPlatform", new Vector2(3.5f, -2.65f), new Vector2(2.3f, .45f));
                var station = Sprite("RepairStation", "station", new Vector2(7.5f, -2.75f), 3).gameObject;
                session.Station = station.AddComponent<RepairStation>();
                session.Station.session = session;
            }
            if (mode == WorkshopMode.Movement)
            {
                Block("Pit", new Vector2(0, -4.5f), new Vector2(19.4f, 1.4f), new Color(.08f, .15f, .18f), -10);
                Ground("Start", new Vector2(-7.5f, -4.05f), new Vector2(4, .5f));
                Ground("Jump_1", new Vector2(-3.6f, -2.95f), new Vector2(2.5f, .5f));
                Ground("Jump_2", new Vector2(-.5f, -1.75f), new Vector2(2.5f, .5f));
                Ground("Jump_3", new Vector2(2.6f, -2.7f), new Vector2(3.7f, .5f));
                Ground("Finish", new Vector2(7, -3.7f), new Vector2(4, .5f));
                var goal = Block("Goal", new Vector2(8, -2.7f), new Vector2(.15f, 1.4f), Mint, 4).gameObject;
                var c = goal.AddComponent<BoxCollider2D>();
                c.isTrigger = true;
                goal.AddComponent<MovementGoal>().session = session;
            }
            if (mode == WorkshopMode.Physics)
                BuildPhysics(session);
            BuildHud(session);
            EditorSceneManager.SaveScene(scene, Root + "Scenes/" + SceneNames[(int)mode] + ".unity");
        }
        static void BuildPhysics(WorkshopSession session)
        {
            var launcher = Sprite("Launcher", "machine", new Vector2(-7.3f, -2.3f), 3).transform;
            session.launcher = new GameObject("LaunchPoint").transform;
            session.launcher.position = new Vector2(-5.8f, -1.2f);
            session.launchVelocity = new Vector2(6, 6);
            var bumper = Block("NaturalOrDesignedBumper", new Vector2(-1.9f, -3.5f), new Vector2(2.4f, .22f), Orange, 2).gameObject;
            bumper.layer = 8;
            bumper.AddComponent<BoxCollider2D>().sharedMaterial = bouncePhysics;
            var b = bumper.AddComponent<PhysicsBumper>();
            b.session = session;
            b.designedVelocity = new Vector2(7, 7);
            Ground("ImpactWall", new Vector2(-8.7f, -.8f), new Vector2(.3f, 2.5f));
            for (int i = 0; i < 3; i++)
            {
                var crate = Sprite("TargetCrate_" + (i + 1), "crate", new Vector2(4.2f, -3.4f + i * .77f), 4).gameObject;
                var body = crate.AddComponent<Rigidbody2D>();
                body.mass = .7f;
                body.gravityScale = 1;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                crate.AddComponent<BoxCollider2D>().size = new Vector2(.74f, .74f);
                var target = crate.AddComponent<PhysicsTarget>();
                target.session = session;
                target.visual = crate.GetComponent<SpriteRenderer>();
                var trigger = new GameObject("ScoreTrigger");
                trigger.transform.SetParent(crate.transform, false);
                trigger.AddComponent<CircleCollider2D>().radius = .55f;
                trigger.GetComponent<CircleCollider2D>().isTrigger = true;
                // Unity forwards child-collider trigger messages to the Rigidbody2D owner.
            }
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
        static Button Button(string label, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var panel = Panel(label, parent, pos, size, new Color(.19f, .28f, .29f, .98f));
            panel.raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            var colors = button.colors;
            colors.highlightedColor = new Color(.7f, .84f, .73f);
            colors.pressedColor = Orange;
            button.colors = colors;
            button.onClick.AddListener(action);
            // Persist callbacks: runtime UnityEvent listeners are not saved with a scene.
            button.onClick.RemoveAllListeners();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            var text = Label("Label", panel.transform, Vector2.zero, size - Vector2.one * 8, 13, Cream, TextAnchor.MiddleCenter);
            text.text = label;
            return button;
        }
        static void BuildHud(WorkshopSession session)
        {
            bool main = session.mode == WorkshopMode.MainGame;
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
            hud.hintFollowsWorld = main;
            session.Hud = hud;
            if (main)
            {
                BuildMainHud(session, canvas, hud);
                return;
            }
            var dim = new Color(.55f, .65f, .62f);
            // Header: lab scenes keep a light, panel-less title; the main game shows no title at all.
            var header = Rect("Header", canvas.transform, Vector2.zero, Vector2.zero);
            hud.subtitle = Label("Subtitle", header, new Vector2(-420, 322), new Vector2(400, 18), 11, Mint);
            hud.title = Label("Title", header, new Vector2(-420, 298), new Vector2(400, 32), 22, Cream);
            hud.title.fontStyle = FontStyle.Bold;
            header.gameObject.SetActive(!main);
            if (main)
            {
                // One compact objective card, top-left. Current step orange, done green, pending dim.
                var card = Panel("ObjectiveCard", canvas.transform, new Vector2(-522, 294), new Vector2(200, 96), new Color(Ink.r, Ink.g, Ink.b, .82f)).transform;
                Panel("Accent", card, new Vector2(-98, 0), new Vector2(3, 96), Orange);
                hud.objective = Label("Objectives", card, new Vector2(4, 8), new Vector2(172, 66), 15, Cream, TextAnchor.UpperLeft);
                hud.objective.lineSpacing = 1.15f;
                hud.status = Label("Bag", card, new Vector2(-48, -34), new Vector2(76, 18), 11, dim);
                hud.goalPips = new Image[3];
                for (int i = 0; i < 3; i++)
                    hud.goalPips[i] = Panel("Carried_" + i, card, new Vector2(4 + i * 18, -34), new Vector2(11, 11), Mint);
            }
            else
            {
                hud.objective = Label("Objectives", canvas.transform, new Vector2(390, 314), new Vector2(440, 26), 16, Cream, TextAnchor.MiddleRight);
                hud.status = Label("Status", canvas.transform, new Vector2(390, 290), new Vector2(440, 20), 13, Orange, TextAnchor.MiddleRight);
                hud.goalPips = new Image[0];
            }
            hud.profileLabel = Label("Profile", canvas.transform, new Vector2(0, 200), new Vector2(1000, 22), 12, Mint, TextAnchor.MiddleCenter);
            var hint = Panel("InteractionHint", canvas.transform, main ? Vector2.zero : new Vector2(0, 150), new Vector2(240, 32), new Color(Ink.r, Ink.g, Ink.b, .9f)).transform;
            hud.hint = Label("Hint", hint, Vector2.zero, new Vector2(232, 28), 15, Orange, TextAnchor.MiddleCenter);
            hud.toast = Label("Toast", canvas.transform, new Vector2(0, -262), new Vector2(900, 28), 15, Cream, TextAnchor.MiddleCenter);
            hud.controls = Label("Controls", canvas.transform, new Vector2(0, -326), new Vector2(1100, 24), 13, new Color(Cream.r, Cream.g, Cream.b, .75f), TextAnchor.MiddleCenter);
            hud.menuHint = Label("MenuHint", canvas.transform, new Vector2(560, -340), new Vector2(140, 18), 11, dim, TextAnchor.MiddleRight);
            hud.menuHint.text = "TAB  ·  MENÜ";
            if (session.mode == WorkshopMode.Movement || session.mode == WorkshopMode.OneButton)
            {
                Button("NORMAL [1]", canvas.transform, new Vector2(-150, 232), new Vector2(130, 26), session.SelectNormal);
                Button(session.mode == WorkshopMode.Movement ? "AĞIR [2]" : "SAKİN [2]", canvas.transform, new Vector2(0, 232), new Vector2(130, 26), session.SelectSecond);
                Button(session.mode == WorkshopMode.Movement ? "HAFİF [3]" : "TELAŞLI [3]", canvas.transform, new Vector2(150, 232), new Vector2(130, 26), session.SelectThird);
            }
            if (session.mode == WorkshopMode.Physics)
                Button("MOD DEĞİŞTİR [T]", canvas.transform, new Vector2(0, 232), new Vector2(200, 26), session.TogglePhysics);
            // Scene navigation / sound / shake / restart live in a Tab menu, not on the play screen.
            var menu = Panel("Menu", canvas.transform, Vector2.zero, new Vector2(400, 290), new Color(Ink.r, Ink.g, Ink.b, .97f));
            menu.raycastTarget = true;
            hud.menuPanel = menu.gameObject;
            Panel("MenuAccent", menu.transform, new Vector2(0, 143), new Vector2(400, 4), Orange);
            Label("MenuTitle", menu.transform, new Vector2(0, 112), new Vector2(360, 30), 18, Cream, TextAnchor.MiddleCenter).text = "MENÜ";
            Button("01 ÜRETİM [F1]", menu.transform, new Vector2(-92, 66), new Vector2(170, 32), session.OpenProduction);
            Button("02 HAREKET [F2]", menu.transform, new Vector2(92, 66), new Vector2(170, 32), session.OpenMovement);
            Button("03 FİZİK [F3]", menu.transform, new Vector2(-92, 26), new Vector2(170, 32), session.OpenPhysics);
            Button("04 ATÖLYE [F4]", menu.transform, new Vector2(92, 26), new Vector2(170, 32), session.OpenMain);
            var sound = Button("SES", menu.transform, new Vector2(-92, -24), new Vector2(170, 32), session.Feedback.ToggleMute);
            hud.soundLabel = sound.GetComponentInChildren<Text>();
            Button("SARSINTI [V]", menu.transform, new Vector2(92, -24), new Vector2(170, 32), session.Feedback.ToggleShake);
            Button(session.mode == WorkshopMode.OneButton ? "YENİDEN" : "YENİDEN [R]", menu.transform, new Vector2(-92, -74), new Vector2(170, 32), session.Restart);
            Button("KAPAT [TAB]", menu.transform, new Vector2(92, -74), new Vector2(170, 32), hud.ToggleMenu);
            menu.gameObject.SetActive(false);
            var victory = Panel("Completion", canvas.transform, new Vector2(0, 32), new Vector2(620, 180), new Color(Ink.r, Ink.g, Ink.b, .94f)).gameObject;
            hud.victoryPanel = victory;
            Panel("CompletionAccent", victory.transform, new Vector2(0, 88), new Vector2(620, 4), Mint);
            hud.victoryTitle = Label("Title", victory.transform, new Vector2(0, 44), new Vector2(580, 42), 26, Mint, TextAnchor.MiddleCenter);
            hud.victoryText = Label("Message", victory.transform, new Vector2(0, 0), new Vector2(580, 36), 16, Cream, TextAnchor.MiddleCenter);
            Button("YENİDEN OYNA", victory.transform, new Vector2(0, -52), new Vector2(200, 34), session.Restart);
            victory.SetActive(false);
        }
    }
}
