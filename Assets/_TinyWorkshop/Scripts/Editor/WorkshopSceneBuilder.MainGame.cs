using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TinyWorkshop.Editor
{
    // 04_MainGame redesign: painted workshop (Art/Main, cut by Tools/redesign_assets.py),
    // sprite-sheet animators, kit-style HUD with bottom-right menus. Lab scenes keep the old builder path.
    public static partial class WorkshopSceneBuilder
    {
        const string MainArt = Root + "Art/Main/", AnimFolder = Root + "Animation/";
        [Serializable] sealed class V2 { public float x, y; public Vector2 V => new Vector2(x, y); }
        [Serializable]
        sealed class Layout
        {
            public V2 machineCap, machineSpark, machineOutlet, machineSize, terminalSize, platformSize;
            public V2[] machineLamps, lamps;
            public float floorTopY, platformTopFromBottom;
        }
        static Layout layout;
        static GameObject robotMainPrefab, machineMainPrefab;
        static Gear gearMainPrefab;
        static Font fontBold, fontSemi;
        static readonly Color InkText = new Color(.23f, .16f, .11f), SoftText = new Color(.42f, .3f, .2f), White = new Color(1, .97f, .9f), OutlineColor = new Color(.07f, .16f, .15f, .95f);
        // Gameplay geometry relative to the painted floor (same heights as the original prototype).
        static readonly Vector3[] MainPlatforms = { new Vector3(-1.52f, 1.125f, 2.8f), new Vector3(.62f, 2.225f, 3.2f), new Vector3(3.37f, 1.375f, 3.1f) };

        static Sprite MainSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(MainArt + name + ".png");
        static Sprite UiSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(MainArt + "UI/" + name + ".png");

        static void CreateMainGameAssets()
        {
            layout = JsonUtility.FromJson<Layout>(File.ReadAllText(MainArt + "layout.json"));
            foreach (var file in Directory.GetFiles(MainArt, "*.png", SearchOption.AllDirectories))
                AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            fontBold = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/Baloo2-ExtraBold.ttf");
            fontSemi = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/Baloo2-SemiBold.ttf");
            Directory.CreateDirectory(AnimFolder);
            AssetDatabase.Refresh();
            var idle = SpriteClip("Robot_Idle", new[] { "robot_idle_0", "robot_idle_1", "robot_idle_0", "robot_idle_5" }, 3, true);
            var walk = SpriteClip("Robot_Walk", Enumerable.Range(0, 6).Select(i => "robot_walk_" + i).ToArray(), 10, true);
            var happy = SpriteClip("Robot_Happy", new[] { "robot_idle_3", "robot_idle_3" }, 3, false);
            var robotController = RobotController(idle, walk, happy);
            var machineController = LoopController("Machine", SpriteClip("Machine_Idle", Enumerable.Range(0, 4).Select(i => "machine_" + i).ToArray(), 4, true));
            var terminalController = LoopController("Terminal", SpriteClip("Terminal_Idle", Enumerable.Range(0, 4).Select(i => "terminal_" + i).ToArray(), 4, true));

            // Robot
            var robot = new GameObject("Robot_Main");
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
            var visual = MainSpriteRenderer("Visual", "robot_idle_0", new Vector2(0, -.44f), 10, robot.transform);
            controller.visual = visual.transform;
            controller.bodySprite = visual;
            controller.animator = Animated(visual.gameObject, robotController);
            controller.profiles = movement;
            controller.carrySprite = MainSprite("gear_part");
            controller.carryOffset = new Vector2(.42f, .5f);
            controller.carryScale = .7f;
            controller.emote = MainSpriteRenderer("HeartBubble", "bubble_heart", new Vector2(.62f, 1.3f), 12, robot.transform);
            controller.emote.transform.localScale = Vector3.one * .8f;
            robotMainPrefab = PrefabUtility.SaveAsPrefabAsset(robot, Root + "Prefabs/Robot_Main.prefab");
            UnityEngine.Object.DestroyImmediate(robot);

            // Gear (silver part, settles on platforms)
            var gear = new GameObject("Gear_Main");
            gear.layer = 9;
            var gearBody = gear.AddComponent<Rigidbody2D>();
            gearBody.gravityScale = 2.2f;
            gearBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            gearBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            var circle = gear.AddComponent<CircleCollider2D>();
            circle.radius = .29f;
            circle.sharedMaterial = gearPhysics;
            var gearComponent = gear.AddComponent<Gear>();
            gearComponent.unreachableAbove = .2f;
            MainSpriteRenderer("Visual", "gear_part", Vector2.zero, 8, gear.transform);
            var pickup = new GameObject("PickupTrigger");
            pickup.layer = 11;
            pickup.transform.SetParent(gear.transform, false);
            pickup.AddComponent<CircleCollider2D>().isTrigger = true;
            pickup.GetComponent<CircleCollider2D>().radius = .5f;
            pickup.AddComponent<GearPickup>().gear = gearComponent;
            gearMainPrefab = PrefabUtility.SaveAsPrefabAsset(gear, Root + "Prefabs/Gear_Main.prefab").GetComponent<Gear>();
            UnityEngine.Object.DestroyImmediate(gear);

            // Production machine: animated body, separate red cap, charge-lamp glows, ready sparks.
            var machine = new GameObject("WorkshopMachine_Main");
            var comp = machine.AddComponent<WorkshopMachine>();
            comp.profiles = production;
            var machineBody = MainSpriteRenderer("Body", "machine_0", Vector2.zero, 3, machine.transform);
            Animated(machineBody.gameObject, machineController);
            comp.bodyVisual = machineBody.transform;
            comp.pressGlow = MainSpriteRenderer("PressGlow", "soft_glow", layout.machineCap.V + new Vector2(0, .3f), 4, machine.transform);
            comp.pressGlow.transform.localScale = Vector3.one * 1.5f;
            comp.pressGlow.color = Color.clear;
            comp.buttonVisual = MainSpriteRenderer("RedButton", "machine_cap", layout.machineCap.V, 5, machine.transform).transform;
            comp.pressDepth = .08f;
            comp.readySpark = MainSpriteRenderer("ReadySpark", "machine_spark", layout.machineSpark.V, 6, machine.transform);
            comp.glowLights = true;
            comp.glowColors = new[] { new Color(.5f, 1, .45f), new Color(1, .9f, .35f), new Color(1, .45f, .35f) };
            comp.chargeLights = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                comp.chargeLights[i] = MainSpriteRenderer("ChargeGlow_" + i, "soft_glow", layout.machineLamps[i].V, 6, machine.transform);
                comp.chargeLights[i].transform.localScale = Vector3.one * .6f;
                comp.chargeLights[i].color = Color.clear;
            }
            var click = machine.AddComponent<BoxCollider2D>();
            click.size = new Vector2(layout.machineSize.x * .92f, 3.7f);
            click.offset = new Vector2(0, 1.85f);
            click.isTrigger = true;
            comp.clickArea = click;
            comp.interactionRange = 2.6f;
            comp.outlet = new GameObject("GearOutlet").transform;
            comp.outlet.SetParent(machine.transform, false);
            // Launch from just behind the conveyor end, above it: clears the low platform's edge.
            comp.outlet.localPosition = layout.machineOutlet.V + new Vector2(-.3f, .2f);
            machineMainPrefab = PrefabUtility.SaveAsPrefabAsset(machine, Root + "Prefabs/WorkshopMachine_Main.prefab");
            UnityEngine.Object.DestroyImmediate(machine);
            terminalAnimator = terminalController;
        }
        static AnimatorController terminalAnimator;

        static SpriteRenderer MainSpriteRenderer(string name, string sprite, Vector2 position, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent)
                go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MainSprite(sprite) ?? UiSprite(sprite);
            sr.sharedMaterial = spriteMaterial;
            sr.sortingOrder = order;
            return sr;
        }
        static Animator Animated(GameObject go, RuntimeAnimatorController controller)
        {
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }
        static AnimationClip SpriteClip(string name, string[] frames, float fps, bool loop)
        {
            string path = AnimFolder + name + ".anim";
            AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { frameRate = fps, name = name };
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i <= frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = MainSprite(frames[Mathf.Min(i, frames.Length - 1)]) };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
        static AnimatorController LoopController(string name, AnimationClip clip)
        {
            string path = AnimFolder + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.layers[0].stateMachine.AddState("Idle").motion = clip;
            return controller;
        }
        // Idle <-> Walk on |velocity.x|, Happy (^^ face) on trigger for pickups / deliveries.
        static AnimatorController RobotController(AnimationClip idle, AnimationClip walk, AnimationClip happy)
        {
            string path = AnimFolder + "Robot.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Happy", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle");
            sIdle.motion = idle;
            var sWalk = sm.AddState("Walk");
            sWalk.motion = walk;
            var sHappy = sm.AddState("Happy");
            sHappy.motion = happy;
            sm.defaultState = sIdle;
            var toWalk = sIdle.AddTransition(sWalk);
            toWalk.hasExitTime = false;
            toWalk.duration = 0;
            toWalk.AddCondition(AnimatorConditionMode.Greater, .5f, "Speed");
            var toIdle = sWalk.AddTransition(sIdle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0;
            toIdle.AddCondition(AnimatorConditionMode.Less, .5f, "Speed");
            var toHappy = sm.AddAnyStateTransition(sHappy);
            toHappy.hasExitTime = false;
            toHappy.duration = 0;
            toHappy.canTransitionToSelf = false;
            toHappy.AddCondition(AnimatorConditionMode.If, 0, "Happy");
            var back = sHappy.AddTransition(sIdle);
            back.hasExitTime = true;
            back.exitTime = 1;
            back.duration = 0;
            return controller;
        }

        // ---------------------------------------------------------------- scene
        static void BuildMainGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float floor = layout.floorTopY;
            var session = new GameObject("WorkshopSession").AddComponent<WorkshopSession>();
            session.mode = WorkshopMode.MainGame;
            session.gearPrefab = gearMainPrefab;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(.16f, .33f, .32f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            session.worldCamera = camera;
            new GameObject("Workshop Light").AddComponent<Light>().type = LightType.Directional;

            var world = new GameObject("World").transform;
            var backdrop = MainSpriteRenderer("WorkshopBackdrop", "backdrop", Vector2.zero, -20, world);
            backdrop.transform.localScale = new Vector3(1, 11.25f / backdrop.sprite.bounds.size.y, 1);

            session.Feedback = new GameObject("Feedback").AddComponent<WorkshopFeedback>();
            session.Feedback.profile = feedback;
            session.Feedback.worldCamera = camera;
            session.Feedback.sparkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/spark.png");
            session.Feedback.puffSprite = MainSprite("soft_glow");
            session.Feedback.clips = Directory.GetFiles(Root + "Audio", "*.wav").Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).Where(c => c.name != "music" && c.name != "ambience").ToArray();
            session.Feedback.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/music.wav");
            session.Feedback.ambienceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/ambience.wav");

            // Painted ceiling lamps get a warm bulb + cone glow that brighten when the workshop wakes up.
            var lights = new GameObject("LampGlows").transform;
            lights.SetParent(world, false);
            session.lamps = new SpriteRenderer[3];
            session.lampGlows = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var at = layout.lamps[i].V;
                session.lamps[i] = MainSpriteRenderer("Bulb_" + i, "soft_glow", at, -5, lights);
                session.lamps[i].transform.localScale = Vector3.one * .9f;
                session.lamps[i].color = new Color(1, .86f, .55f, .12f);
                session.lampGlows[i] = MainSpriteRenderer("Cone_" + i, "soft_glow", at + new Vector2(0, -1.9f), -4, lights);
                session.lampGlows[i].transform.localScale = new Vector3(3.2f, 4.2f, 1);
                session.lampGlows[i].color = new Color(1, .78f, .4f, .05f);
            }

            var level = new GameObject("Level").transform;
            level.SetParent(world, false);
            Ground("LeftWall", new Vector2(-9.8f, 0), new Vector2(.3f, 20), false);
            Ground("RightWall", new Vector2(9.8f, 0), new Vector2(.3f, 20), false);
            Ground("Floor", new Vector2(0, floor - 1), new Vector2(20, 2), false);
            string[] names = { "LowPlatform", "MiddlePlatform", "RightPlatform" };
            var drops = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                var p = MainPlatforms[i];
                float top = floor + p.y;
                var go = new GameObject(names[i]);
                go.layer = 8;
                go.transform.SetParent(level, false);
                go.transform.position = new Vector2(p.x, top - .225f);
                go.AddComponent<BoxCollider2D>().size = new Vector2(p.z, .45f);
                var sr = MainSpriteRenderer("Platform", "platform", new Vector2(0, .225f + .02f), 1, go.transform);
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(p.z + .06f, layout.platformSize.y);
            }
            // Launch order: low, right, middle -> the player has to climb for the last one.
            drops[0] = new Vector2(MainPlatforms[0].x, floor + MainPlatforms[0].y + .31f);
            drops[1] = new Vector2(MainPlatforms[2].x, floor + MainPlatforms[2].y + .31f);
            drops[2] = new Vector2(MainPlatforms[1].x, floor + MainPlatforms[1].y + .31f);
            session.gearDrops = drops;
            session.safeGearPoint = new Vector2(5.3f, floor + .4f);
            foreach (var n in new[] { "LeftWall", "RightWall", "Floor" })
                GameObject.Find(n).transform.SetParent(level, true);

            var machine = (GameObject)PrefabUtility.InstantiatePrefab(machineMainPrefab);
            machine.transform.position = new Vector3(-5.29f, floor);
            session.Machine = machine.GetComponent<WorkshopMachine>();
            session.Machine.session = session;

            var station = MainSpriteRenderer("DeliveryTerminal", "terminal_0", new Vector2(6.81f, floor), 3).gameObject;
            Animated(station, terminalAnimator);
            session.Station = station.AddComponent<RepairStation>();
            session.Station.session = session;
            session.Station.interactionRange = 2;
            session.Station.callout = MainSpriteRenderer("TruckBubble", "truck_bubble", new Vector2(1.25f, 2.75f), 7, station.transform);
            session.Station.callout.transform.localScale = Vector3.one * .55f;
            session.Station.confirm = MainSpriteRenderer("DeliveredCheck", "node_done", new Vector2(-.2f, 3.3f), 9, station.transform);

            var robot = (GameObject)PrefabUtility.InstantiatePrefab(robotMainPrefab);
            robot.transform.position = new Vector3(-8.6f, floor + .46f);
            session.Robot = robot.GetComponent<RobotController2D>();
            session.Robot.session = session;

            BuildHud(session);
            EditorSceneManager.SaveScene(scene, Root + "Scenes/04_MainGame.unity");
        }

        // ---------------------------------------------------------------- HUD
        static RectTransform Anchored(RectTransform rt, Vector2 anchor, Vector2 pivot)
        {
            var pos = rt.anchoredPosition;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            return rt;
        }
        static Image Img(string name, Transform parent, string sprite, Vector2 pos, Vector2 size, float slicedPpu = 0)
        {
            var image = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
            image.sprite = UiSprite(sprite) ?? MainSprite(sprite);
            image.raycastTarget = false;
            if (slicedPpu > 0)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = slicedPpu;
            }
            else
                image.preserveAspect = true;
            return image;
        }
        static Text T(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align, bool bold = true, bool outline = false)
        {
            var text = Label(name, parent, pos, size, fontSize, color, align);
            text.font = bold ? fontBold : fontSemi;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var o = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = OutlineColor;
                o.effectDistance = new Vector2(1.6f, -1.6f);
            }
            return text;
        }
        static Button Clickable(Image image, UnityAction action, bool press = true)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1, 1, .92f);
            colors.pressedColor = new Color(.85f, .82f, .75f);
            colors.fadeDuration = .06f;
            button.colors = colors;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            if (press)
                image.gameObject.AddComponent<UiPress>();
            return button;
        }
        static Text KeyButton(string label, Transform parent, Vector2 pos, Vector2 size, UnityAction action, int fontSize = 17)
        {
            var image = Img(label, parent, "keycap_wide", pos, size, 1.3f);
            Clickable(image, action);
            var text = T("Label", image.transform, new Vector2(0, 1), size, fontSize, InkText, TextAnchor.MiddleCenter);
            text.text = label;
            return text;
        }
        static RectTransform Bar(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var track = Img(name, parent, "bar_track", pos, size, 28f / size.y);
            var fill = Img("Fill", track.transform, "bar_fill", Vector2.zero, Vector2.zero, 28f / size.y).rectTransform;
            fill.anchorMin = new Vector2(0, 0);
            fill.anchorMax = new Vector2(0, 1);
            fill.pivot = new Vector2(0, .5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(size.y, 0);
            return fill;
        }
        static GameObject Window(string name, Transform parent, string sprite, Vector2 size, WorkshopHud hud, Vector2 closeAt)
        {
            var window = Img(name, parent, sprite, new Vector2(0, 20), size, sprite == "window_plain" ? 1.2f : 0);
            window.raycastTarget = true;
            var close = Img("Close", window.transform, "keycap", closeAt, new Vector2(40, 42), 1.2f);
            Clickable(close, hud.ClosePanels);
            T("X", close.transform, new Vector2(0, 2), new Vector2(40, 40), 26, InkText, TextAnchor.MiddleCenter).text = "×";
            window.gameObject.SetActive(false);
            return window.gameObject;
        }

        static void BuildMainHud(WorkshopSession session, Canvas canvas, WorkshopHud hud)
        {
            var root = canvas.transform;
            hud.floaterFont = fontBold;
            hud.goalPips = new Image[0];
            hud.nodeDone = UiSprite("node_done");
            hud.nodeCurrent = UiSprite("node_current");
            hud.nodeEmpty = UiSprite("node_empty");

            // Top-left: objective card.
            var card = Anchored(Img("ObjectiveCard", root, "card", new Vector2(8, -6), new Vector2(304, 102)).rectTransform, new Vector2(0, 1), new Vector2(0, 1));
            var cardContent = Anchored(Rect("Content", card, Vector2.zero, new Vector2(304, 102)), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            hud.objectiveCard = card;
            hud.objectiveTitle = T("Title", cardContent, new Vector2(40, 20), new Vector2(196, 26), 18, InkText, TextAnchor.MiddleLeft);
            hud.objectiveStep = T("Step", cardContent, new Vector2(40, -1), new Vector2(196, 22), 14, SoftText, TextAnchor.MiddleLeft, false);
            hud.objectiveFill = Bar("Progress", cardContent, new Vector2(38, -25), new Vector2(188, 16));

            // Top-centre: step track (Üret / Topla / Teslim) ending in the reward gift.
            var stage = Anchored(Rect("StageTrack", root, new Vector2(0, -38), new Vector2(460, 60)), new Vector2(.5f, 1), new Vector2(.5f, .5f));
            var track = Img("Track", stage, "stage_track", new Vector2(50, 0), new Vector2(300, 40));
            track.preserveAspect = false;
            var pill = Img("StagePill", stage, "stage_label", new Vector2(-150, 0), new Vector2(136, 48), 92f / 48);
            hud.stageLabel = T("Label", pill.transform, new Vector2(0, 1), new Vector2(130, 40), 19, White, TextAnchor.MiddleCenter, true, true);
            hud.stageNodes = new Image[3];
            string[] stepNames = { "Üret", "Topla", "Teslim" };
            for (int i = 0; i < 3; i++)
            {
                hud.stageNodes[i] = Img("Node_" + i, stage, "node_empty", new Vector2(-40 + i * 78, 0), new Vector2(42, 42));
                T("Caption", hud.stageNodes[i].transform, new Vector2(0, -32), new Vector2(80, 18), 12, White, TextAnchor.MiddleCenter, true, true).text = stepNames[i];
            }
            hud.gift = Img("Gift", stage, "gift", new Vector2(198, 2), new Vector2(54, 48)).rectTransform;

            // Top-right: coins + carried gears; the "+" opens the related window.
            var coin = Anchored(Img("CoinCounter", root, "counter_coin", new Vector2(-180, -8), new Vector2(172, 52)).rectTransform, new Vector2(1, 1), new Vector2(1, 1));
            hud.coinCounter = coin;
            hud.coinText = T("Value", coin, new Vector2(4, 1), new Vector2(90, 40), 24, White, TextAnchor.MiddleCenter, true, true);
            var coinPlus = Img("Plus", coin, "card", new Vector2(58, 1), new Vector2(38, 38));
            coinPlus.color = Color.clear;
            Clickable(coinPlus, hud.OpenUpgrades, false);
            var gearCounter = Anchored(Img("GearCounter", root, "counter_gear", new Vector2(-10, -8), new Vector2(158, 53)).rectTransform, new Vector2(1, 1), new Vector2(1, 1));
            hud.gearCounter = gearCounter;
            hud.gearText = T("Value", gearCounter, new Vector2(4, 1), new Vector2(70, 40), 24, White, TextAnchor.MiddleCenter, true, true);
            var gearPlus = Img("Plus", gearCounter, "card", new Vector2(52, 1), new Vector2(38, 38));
            gearPlus.color = Color.clear;
            Clickable(gearPlus, hud.OpenMissions, false);

            // Bottom bar: title, control chips, menu buttons.
            var bar = Img("BottomBar", root, "bottom_bar", Vector2.zero, Vector2.zero, 1.75f).rectTransform;
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(.5f, 0);
            bar.anchoredPosition = new Vector2(0, 4);
            bar.sizeDelta = new Vector2(-12, 72);
            var left = Anchored(Rect("Left", bar, new Vector2(0, 36), new Vector2(10, 10)), new Vector2(0, 0), new Vector2(0, .5f));
            Img("Logo", left, "logo_gear", new Vector2(30, 0), new Vector2(40, 40));
            var brand = T("Brand", left, new Vector2(56, 1), new Vector2(200, 40), 25, White, TextAnchor.MiddleLeft, true, true);
            brand.rectTransform.pivot = new Vector2(0, .5f);
            brand.text = "Tiny Workshop";
            var divider = Panel("Divider", left, new Vector2(262, 0), new Vector2(3, 36), new Color(.42f, .7f, .64f, .8f));
            float x = 284;
            x = Chip(left, x, new[] { "mouse" }, "Tıkla: Üret");
            x = Chip(left, x, new[] { "A", "D" }, "Hareket");
            x = Chip(left, x, new[] { "SPACE" }, "Zıpla");
            x = Chip(left, x, new[] { "E" }, "Etkileşim");
            Chip(left, x, new[] { "TAB" }, "Menü");
            var right = Anchored(Rect("Menu", bar, new Vector2(0, 36), new Vector2(10, 10)), new Vector2(1, 0), new Vector2(1, .5f));
            (string sprite, UnityAction action)[] menu = { ("btn_home", hud.OpenHome), ("btn_build", hud.OpenUpgrades), ("btn_box", hud.OpenMissions), ("btn_settings", hud.OpenSettings) };
            for (int i = 0; i < menu.Length; i++)
                Clickable(Img(menu[i].sprite.Replace("btn_", "Button_"), right, menu[i].sprite, new Vector2(-226 + i * 64, 2), new Vector2(60, 58)), menu[i].action);

            // World hint pill + toast.
            var hint = Img("InteractionHint", root, "stage_label", Vector2.zero, new Vector2(240, 40), 92f / 40);
            hud.hint = T("Hint", hint.transform, new Vector2(0, 1), new Vector2(232, 34), 17, new Color(1, .9f, .62f), TextAnchor.MiddleCenter, true, true);
            hud.toast = T("Toast", root, new Vector2(0, -240), new Vector2(900, 30), 18, White, TextAnchor.MiddleCenter, true, true);

            // Missions window (box button / gear +)
            var missions = Window("MissionsWindow", root, "window_missions", new Vector2(520, 336), hud, new Vector2(262, 72));
            hud.missionsPanel = missions;
            T("TabActive", missions.transform, new Vector2(-104, 122), new Vector2(200, 40), 23, InkText, TextAnchor.MiddleCenter).text = "Görevler";
            var toUpgrades = Img("TabUpgrades", missions.transform, "card", new Vector2(136, 116), new Vector2(220, 64));
            toUpgrades.color = Color.clear;
            Clickable(toUpgrades, hud.OpenUpgrades, false);
            T("Label", toUpgrades.transform, Vector2.zero, new Vector2(220, 40), 19, White, TextAnchor.MiddleCenter, true, true).text = "Geliştirmeler";
            string[] rowNames = { "Dişli üret", "Dişlileri topla", "Teslim et" };
            string[] rowIcons = { "icon_gear", "icon_crate", "icon_truck" };
            hud.missionFills = new RectTransform[3];
            hud.missionCounts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var row = Img("Row_" + i, missions.transform, "row", new Vector2(0, 26 - i * 68), new Vector2(474, 62), 1.25f).rectTransform;
                Img("Icon", row, rowIcons[i], new Vector2(-202, 0), new Vector2(46, 42));
                T("Name", row, new Vector2(-52, 11), new Vector2(220, 24), 17, InkText, TextAnchor.MiddleLeft).text = rowNames[i];
                hud.missionCounts[i] = T("Count", row, new Vector2(170, 11), new Vector2(90, 24), 16, SoftText, TextAnchor.MiddleRight);
                hud.missionFills[i] = Bar("Progress", row, new Vector2(30, -12), new Vector2(370, 14));
            }

            // Upgrades window (hammer button / coin +): placeholder until the upgrade GDD lands.
            var upgrades = Window("UpgradesWindow", root, "window_upgrades", new Vector2(520, 322), hud, new Vector2(262, 70));
            hud.upgradesPanel = upgrades;
            var toMissions = Img("TabMissions", upgrades.transform, "card", new Vector2(-146, 109), new Vector2(214, 62));
            toMissions.color = Color.clear;
            Clickable(toMissions, hud.OpenMissions, false);
            T("Label", toMissions.transform, Vector2.zero, new Vector2(214, 40), 19, White, TextAnchor.MiddleCenter, true, true).text = "Görevler";
            T("TabActive", upgrades.transform, new Vector2(128, 116), new Vector2(240, 40), 23, InkText, TextAnchor.MiddleCenter).text = "Geliştirmeler";
            (string icon, string name)[] ups = { ("icon_gear", "Üretim Hızı"), ("icon_box", "Çanta Kapasitesi") };
            for (int i = 0; i < ups.Length; i++)
            {
                var row = Img("Upgrade_" + i, upgrades.transform, "row", new Vector2(0, 22 - i * 76), new Vector2(474, 68), 1.25f).rectTransform;
                Img("Icon", row, ups[i].icon, new Vector2(-200, 0), new Vector2(50, 48));
                T("Name", row, new Vector2(-28, 0), new Vector2(240, 30), 19, InkText, TextAnchor.MiddleLeft).text = ups[i].name;
                var chip = Img("Level", row, "lv_chip", new Vector2(110, 0), new Vector2(64, 34), 1);
                T("Lv", chip.transform, new Vector2(0, 1), new Vector2(64, 30), 15, SoftText, TextAnchor.MiddleCenter).text = "Sv. 1";
                Clickable(Img("UpgradeButton", row, "btn_up", new Vector2(190, 0), new Vector2(52, 50)), hud.UpgradeLocked);
            }
            T("Note", upgrades.transform, new Vector2(0, -122), new Vector2(460, 24), 15, SoftText, TextAnchor.MiddleCenter, false).text = "Geliştirme sistemi yakında açılıyor";

            // Home window (house button / Tab): scenes + restart.
            var home = Window("HomeWindow", root, "window_plain", new Vector2(430, 300), hud, new Vector2(200, 135));
            hud.menuPanel = home;
            T("Title", home.transform, new Vector2(0, 104), new Vector2(380, 40), 28, InkText, TextAnchor.MiddleCenter).text = "Atölye";
            KeyButton("01 Üretim  [F1]", home.transform, new Vector2(-98, 46), new Vector2(184, 44), session.OpenProduction);
            KeyButton("02 Hareket  [F2]", home.transform, new Vector2(98, 46), new Vector2(184, 44), session.OpenMovement);
            KeyButton("03 Fizik  [F3]", home.transform, new Vector2(-98, -6), new Vector2(184, 44), session.OpenPhysics);
            KeyButton("04 Atölye  [F4]", home.transform, new Vector2(98, -6), new Vector2(184, 44), session.OpenMain);
            KeyButton("Yeniden Başla  [R]", home.transform, new Vector2(0, -74), new Vector2(380, 46), session.Restart, 18);

            // Settings window (gear button).
            var settings = Window("SettingsWindow", root, "window_plain", new Vector2(380, 250), hud, new Vector2(175, 110));
            hud.settingsPanel = settings;
            T("Title", settings.transform, new Vector2(0, 80), new Vector2(340, 40), 28, InkText, TextAnchor.MiddleCenter).text = "Ayarlar";
            hud.soundLabel = KeyButton("Sound", settings.transform, new Vector2(0, 18), new Vector2(280, 46), session.Feedback.ToggleMute, 18);
            hud.shakeLabel = KeyButton("Shake", settings.transform, new Vector2(0, -40), new Vector2(280, 46), hud.ToggleShake, 18);

            // Completion window.
            var victory = Img("Completion", root, "window_plain", new Vector2(0, 40), new Vector2(540, 230), 1.2f).gameObject;
            victory.GetComponent<Image>().raycastTarget = true;
            hud.victoryPanel = victory;
            Img("Gift", victory.transform, "gift", new Vector2(0, 112), new Vector2(84, 74));
            hud.victoryTitle = T("Title", victory.transform, new Vector2(0, 42), new Vector2(500, 44), 32, new Color(.2f, .52f, .3f), TextAnchor.MiddleCenter);
            hud.victoryText = T("Message", victory.transform, new Vector2(0, 2), new Vector2(500, 30), 18, SoftText, TextAnchor.MiddleCenter, false);
            KeyButton("Yeniden Oyna", victory.transform, new Vector2(0, -60), new Vector2(220, 48), session.Restart, 19);
            victory.SetActive(false);
        }
        // One control hint: key caps (or the mouse icon) followed by a label. Returns the next x.
        static float Chip(Transform parent, float x, string[] keys, string label)
        {
            foreach (var key in keys)
            {
                if (key == "mouse")
                {
                    Img("Mouse", parent, "icon_mouse", new Vector2(x + 12, 0), new Vector2(24, 30));
                    x += 30;
                    continue;
                }
                float w = key.Length == 1 ? 30 : 16 + key.Length * 11;
                var cap = Img("Key_" + key, parent, key.Length == 1 ? "keycap" : "keycap_wide", new Vector2(x + w / 2, 0), new Vector2(w, 32), 1.6f);
                T("K", cap.transform, new Vector2(0, 1), new Vector2(w, 30), key.Length == 1 ? 16 : 13, InkText, TextAnchor.MiddleCenter).text = key;
                x += w + 3;
            }
            var text = T("Label_" + label, parent, new Vector2(x + 5, 1), new Vector2(160, 30), 16, White, TextAnchor.MiddleLeft, false, true);
            text.rectTransform.pivot = new Vector2(0, .5f);
            text.text = label;
            return x + 5 + text.preferredWidth + 18;
        }
    }
}
