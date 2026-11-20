using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TinyWorkshop.Editor
{
    // Lab scenes (01-03) on the same painted art and kit-style HUD as 04_MainGame.
    public static partial class WorkshopSceneBuilder
    {
        // Movement course keeps the tuned prototype geometry, lifted so the start step sits just above
        // the painted floor (PlayMode tests use the same offset).
        const float MovementLift = .96f;
        static readonly Color GoalGlow = new Color(.6f, 1, .65f, .5f), BumperTint = new Color(1, .78f, .55f);

        // Camera, painted backdrop, lamp glows, feedback, walls + floor. Shared by every scene.
        static WorkshopSession BeginPaintedScene(WorkshopMode mode, Gear gear, out Transform level)
        {
            float floor = layout.floorTopY;
            var session = new GameObject("WorkshopSession").AddComponent<WorkshopSession>();
            session.gameObject.AddComponent<WorkshopHotkeys>();
            session.mode = mode;
            session.movementProfiles = movement;
            session.productionProfiles = production;
            session.gearPrefab = gear;
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

            level = new GameObject("Level").transform;
            level.SetParent(world, false);
            Ground("LeftWall", new Vector2(-9.8f, 0), new Vector2(.3f, 20), false, level);
            Ground("RightWall", new Vector2(9.8f, 0), new Vector2(.3f, 20), false, level);
            Ground("Floor", new Vector2(0, floor - 1), new Vector2(20, 2), false, level);
            return session;
        }

        static void BuildLabScene(WorkshopMode mode)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float floor = layout.floorTopY;
            var session = BeginPaintedScene(mode, gearLabPrefab, out var level);
            if (mode == WorkshopMode.OneButton)
            {
                PlaceMachine(session, new Vector3(-3.2f, floor));
            }
            else if (mode == WorkshopMode.Movement)
                BuildMovement(session, level);
            else
                BuildPhysics(session, level, floor);
            BuildHud(session);
            EditorSceneManager.SaveScene(scene, Root + "Scenes/" + SceneNames[(int)mode] + ".unity");
        }

        // Prefab instances get their scene references (session / feedback) after placing.
        static void PlaceRobot(WorkshopSession session, Vector3 position)
        {
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(robotMainPrefab);
            robot.transform.position = position;
            robot.GetComponent<RobotPresentation>().feedback = session.Feedback;
            robot.GetComponent<RobotCarryStack>().session = session;
            session.Robot = robot.GetComponent<RobotController2D>();
        }

        static void PlaceMachine(WorkshopSession session, Vector3 position)
        {
            var machine = (GameObject)PrefabUtility.InstantiatePrefab(machineMainPrefab);
            machine.transform.position = position;
            session.Machine = machine.GetComponent<WorkshopMachine>();
            session.Machine.session = session;
        }

        static void BuildMovement(WorkshopSession session, Transform level)
        {
            float l = MovementLift;
            Ground("Start", new Vector2(-7.5f, -4.05f + l), new Vector2(4, .5f), true, level);
            Ground("Jump_1", new Vector2(-3.6f, -2.95f + l), new Vector2(2.5f, .5f), true, level);
            Ground("Jump_2", new Vector2(-.5f, -1.75f + l), new Vector2(2.5f, .5f), true, level);
            Ground("Jump_3", new Vector2(2.6f, -2.7f + l), new Vector2(3.7f, .5f), true, level);
            Ground("Finish", new Vector2(7, -3.7f + l), new Vector2(4, .5f), true, level);
            // Goal: the reward gift on the finish platform.
            var goal = new GameObject("Goal");
            goal.transform.position = new Vector2(8, -2.7f + l);
            var trigger = goal.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(.3f, 1.4f);
            trigger.isTrigger = true;
            var glow = MainSpriteRenderer("Glow", "soft_glow", new Vector2(0, -.1f), 3, goal.transform);
            glow.transform.localScale = Vector3.one * 1.6f;
            glow.color = GoalGlow;
            var gift = MainSpriteRenderer("Gift", "gift", new Vector2(0, -.15f), 4, goal.transform);
            gift.transform.localScale = Vector3.one * .85f;
            var component = goal.AddComponent<MovementGoal>();
            component.session = session;
            component.visual = gift.transform;
            PlaceRobot(session, new Vector3(-8, -3.3f + l));
        }

        static void BuildPhysics(WorkshopSession session, Transform level, float floor)
        {
            // Same throw / bumper / crate geometry as the prototype, lifted onto the painted floor.
            float l = floor + 3.8f;
            var launcher = MainSpriteRenderer("Launcher", "machine_0", new Vector2(-7.45f, floor), 3);
            launcher.transform.localScale = Vector3.one * .75f;
            Animated(launcher.gameObject, machineAnimator);
            MainSpriteRenderer("Cap", "machine_cap", layout.machineCap.V, 4, launcher.transform);
            session.launcher = new GameObject("LaunchPoint").transform;
            session.launcher.position = new Vector2(-5.8f, -1.2f + l);
            session.launchVelocity = new Vector2(6, 6);
            session.safeGearPoint = new Vector2(-3.8f, floor + .4f);
            var bumper = new GameObject("NaturalOrDesignedBumper");
            bumper.layer = 8;
            bumper.transform.SetParent(level, false);
            bumper.transform.position = new Vector2(-1.9f, -3.5f + l);
            var box = bumper.AddComponent<BoxCollider2D>();
            box.size = new Vector2(2.4f, .22f);
            box.sharedMaterial = bouncePhysics;
            var plank = MainSpriteRenderer("Plank", "platform", new Vector2(0, .13f), 2, bumper.transform);
            plank.drawMode = SpriteDrawMode.Sliced;
            plank.size = new Vector2(2.46f, .45f);
            plank.color = BumperTint;
            var b = bumper.AddComponent<PhysicsBumper>();
            b.session = session;
            b.designedVelocity = new Vector2(7, 7);
            Ground("ImpactWall", new Vector2(-8.7f, -.8f + l), new Vector2(.3f, 2.5f), false, level);
            for (int i = 0; i < 3; i++)
            {
                var crate = MainSpriteRenderer("TargetCrate_" + (i + 1), "icon_crate", new Vector2(4.2f, -3.4f + l + i * .77f), 4);
                crate.transform.localScale = Vector3.one * (.78f / crate.sprite.bounds.size.x);
                var body = crate.gameObject.AddComponent<Rigidbody2D>();
                body.mass = .7f;
                body.gravityScale = 1;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                // Collider sizes are local: undo the sprite scale so the physics box stays .74 units.
                float s = crate.transform.localScale.x;
                crate.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * (.74f / s);
                var target = crate.gameObject.AddComponent<PhysicsTarget>();
                target.session = session;
                target.visual = crate;
                var trigger = new GameObject("ScoreTrigger");
                trigger.transform.SetParent(crate.transform, false);
                var circle = trigger.AddComponent<CircleCollider2D>();
                circle.radius = .55f / s;
                circle.isTrigger = true;
                // Unity forwards child-collider trigger messages to the Rigidbody2D owner.
            }
        }

        // ---------------------------------------------------------------- lab HUD
        static readonly string[] LabTitles = { "Üretim Tezgahı", "Robot Parkuru", "Fizik Laboratuvarı" };
        static readonly string[] LabBadges = { "01 · Tek Tuş", "02 · Hareket", "03 · Fizik" };

        static void BuildLabHud(WorkshopSession session, Canvas canvas, WorkshopHud hud)
        {
            var root = canvas.transform;
            var mode = session.mode;
            hud.floaterFont = fontBold;
            hud.goalPips = new Image[0];

            // Top-left: objective card (title, current goal, progress bar) + status line under it.
            var card = Anchored(Img("ObjectiveCard", root, "card", new Vector2(8, -6), new Vector2(304, 102)).rectTransform, new Vector2(0, 1), new Vector2(0, 1));
            var content = Anchored(Rect("Content", card, Vector2.zero, new Vector2(304, 102)), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            hud.title = T("Title", content, new Vector2(40, 20), new Vector2(196, 26), 18, InkText, TextAnchor.MiddleLeft);
            hud.objective = T("Objective", content, new Vector2(40, -1), new Vector2(196, 22), 14, SoftText, TextAnchor.MiddleLeft, false);
            hud.objectiveFill = Bar("Progress", content, new Vector2(38, -25), new Vector2(188, 16));
            hud.status = T("Status", root, new Vector2(30, -122), new Vector2(280, 24), 16, White, TextAnchor.MiddleLeft, true, true);
            Anchored(hud.status.rectTransform, new Vector2(0, 1), new Vector2(0, .5f));

            // Top-centre: lab badge, profile / mode buttons, profile description.
            var top = Anchored(Rect("Lab", root, new Vector2(0, -38), new Vector2(10, 10)), new Vector2(.5f, 1), new Vector2(.5f, .5f));
            var badge = Img("Badge", top, "stage_label", Vector2.zero, new Vector2(196, 48), 92f / 48);
            hud.subtitle = T("Label", badge.transform, new Vector2(0, 1), new Vector2(190, 40), 19, White, TextAnchor.MiddleCenter, true, true);
            if (mode == WorkshopMode.Physics)
                KeyButton("Mod Değiştir  [T]", top, new Vector2(0, -56), new Vector2(230, 42), session.TogglePhysics);
            else
            {
                string[] names = mode == WorkshopMode.Movement ? new[] { "Normal", "Ağır", "Hafif" } : new[] { "Normal", "Sakin", "Telaşlı" };
                UnityAction[] actions = { session.SelectNormal, session.SelectSecond, session.SelectThird };
                for (int i = 0; i < 3; i++)
                    KeyButton($"{names[i]}  [{i + 1}]", top, new Vector2(-160 + i * 160, -56), new Vector2(150, 42), actions[i]);
            }
            hud.profileLabel = T("Profile", top, new Vector2(0, -96), new Vector2(700, 24), 15, new Color(1, .9f, .62f), TextAnchor.MiddleCenter, true, true);

            // Top-right: quick scene tabs, the current one lit.
            var tabs = Anchored(Rect("SceneTabs", root, new Vector2(-12, -34), new Vector2(10, 10)), new Vector2(1, 1), new Vector2(1, .5f));
            UnityAction[] scenes = { session.OpenProduction, session.OpenMovement, session.OpenPhysics, session.OpenMain };
            for (int i = 0; i < 4; i++)
            {
                var label = KeyButton("0" + (i + 1), tabs, new Vector2(-206 + i * 62, 0), new Vector2(56, 48), scenes[i], 18);
                if (i == (int)mode)
                    label.transform.parent.GetComponent<Image>().color = new Color(1, .8f, .45f);
            }

            // Bottom bar: brand + this lab's controls + home / settings.
            (string[] keys, string label)[] chips = mode == WorkshopMode.OneButton
                ? new[] { (new[] { "SPACE" }, "Bas / Üret"), (new[] { "1", "2", "3" }, "Ritim"), (new[] { "TAB" }, "Menü") }
                : mode == WorkshopMode.Movement
                ? new[] { (new[] { "A", "D" }, "Hareket"), (new[] { "SPACE" }, "Zıpla"), (new[] { "1", "2", "3" }, "His"), (new[] { "G" }, "Yardım"), (new[] { "TAB" }, "Menü") }
                : new[] { (new[] { "SPACE" }, "Fırlat"), (new[] { "T" }, "Fizik modu"), (new[] { "R" }, "Yeniden"), (new[] { "TAB" }, "Menü") };
            BottomBar(root, chips, new (string, UnityAction)[] { ("btn_home", hud.OpenHome), ("btn_settings", hud.OpenSettings) });

            // Fixed hint pill above the play area + toast.
            var hint = Img("InteractionHint", root, "stage_label", new Vector2(0, 168), new Vector2(240, 40), 92f / 40);
            hud.hint = T("Hint", hint.transform, new Vector2(0, 1), new Vector2(232, 34), 17, new Color(1, .9f, .62f), TextAnchor.MiddleCenter, true, true);
            hud.toast = T("Toast", root, new Vector2(0, -240), new Vector2(900, 30), 18, White, TextAnchor.MiddleCenter, true, true);

            HomeWindow(session, root, hud);
            SettingsWindow(session, root, hud);
            UnityAction next = mode == WorkshopMode.OneButton ? session.OpenMovement : mode == WorkshopMode.Movement ? session.OpenPhysics : session.OpenMain;
            string nextName = mode == WorkshopMode.OneButton ? "02 Hareket" : mode == WorkshopMode.Movement ? "03 Fizik" : "04 Atölye";
            CompletionWindow(session, root, hud, "Sonraki: " + nextName, next);
        }

        // ---------------------------------------------------------------- shared kit pieces
        static void BottomBar(Transform root, (string[] keys, string label)[] chips, (string sprite, UnityAction action)[] buttons)
        {
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
            Panel("Divider", left, new Vector2(262, 0), new Vector2(3, 36), new Color(.42f, .7f, .64f, .8f));
            float x = 284;
            foreach (var chip in chips)
                x = Chip(left, x, chip.keys, chip.label);
            var right = Anchored(Rect("Menu", bar, new Vector2(0, 36), new Vector2(10, 10)), new Vector2(1, 0), new Vector2(1, .5f));
            for (int i = 0; i < buttons.Length; i++)
                Clickable(Img(buttons[i].sprite.Replace("btn_", "Button_"), right, buttons[i].sprite, new Vector2(-34 - (buttons.Length - 1 - i) * 64, 2), new Vector2(60, 58)), buttons[i].action);
        }
        // Home window (house button / Tab): scenes + restart.
        static void HomeWindow(WorkshopSession session, Transform root, WorkshopHud hud)
        {
            var home = Window("HomeWindow", root, "window_plain", new Vector2(430, 300), hud, new Vector2(200, 135));
            hud.menuPanel = home;
            T("Title", home.transform, new Vector2(0, 104), new Vector2(380, 40), 28, InkText, TextAnchor.MiddleCenter).text = "Atölye";
            KeyButton("01 Üretim  [F1]", home.transform, new Vector2(-98, 46), new Vector2(184, 44), session.OpenProduction);
            KeyButton("02 Hareket  [F2]", home.transform, new Vector2(98, 46), new Vector2(184, 44), session.OpenMovement);
            KeyButton("03 Fizik  [F3]", home.transform, new Vector2(-98, -6), new Vector2(184, 44), session.OpenPhysics);
            KeyButton("04 Atölye  [F4]", home.transform, new Vector2(98, -6), new Vector2(184, 44), session.OpenMain);
            // 01 has no R shortcut: Space is its only key.
            KeyButton(session.mode == WorkshopMode.OneButton ? "Yeniden Başla" : "Yeniden Başla  [R]", home.transform, new Vector2(0, -74), new Vector2(380, 46), session.Restart, 18);
        }
        // Settings window (gear button).
        static void SettingsWindow(WorkshopSession session, Transform root, WorkshopHud hud)
        {
            var settings = Window("SettingsWindow", root, "window_plain", new Vector2(380, 250), hud, new Vector2(175, 110));
            hud.settingsPanel = settings;
            T("Title", settings.transform, new Vector2(0, 80), new Vector2(340, 40), 28, InkText, TextAnchor.MiddleCenter).text = "Ayarlar";
            hud.soundLabel = KeyButton("Sound", settings.transform, new Vector2(0, 18), new Vector2(280, 46), session.Feedback.ToggleMute, 18);
            hud.shakeLabel = KeyButton("Shake", settings.transform, new Vector2(0, -40), new Vector2(280, 46), hud.ToggleShake, 18);
        }
        // Completion window: replay, plus an optional "next" button (labs).
        static void CompletionWindow(WorkshopSession session, Transform root, WorkshopHud hud, string nextLabel = null, UnityAction next = null)
        {
            var victory = Img("Completion", root, "window_plain", new Vector2(0, 40), new Vector2(540, 230), 1.2f).gameObject;
            victory.GetComponent<Image>().raycastTarget = true;
            hud.victoryPanel = victory;
            Img("Gift", victory.transform, "gift", new Vector2(0, 112), new Vector2(84, 74));
            hud.victoryTitle = T("Title", victory.transform, new Vector2(0, 42), new Vector2(500, 44), 32, new Color(.2f, .52f, .3f), TextAnchor.MiddleCenter);
            hud.victoryText = T("Message", victory.transform, new Vector2(0, 2), new Vector2(500, 30), 18, SoftText, TextAnchor.MiddleCenter, false);
            if (next == null)
                KeyButton("Yeniden Oyna", victory.transform, new Vector2(0, -60), new Vector2(220, 48), session.Restart, 19);
            else
            {
                KeyButton("Yeniden Dene", victory.transform, new Vector2(-122, -60), new Vector2(210, 48), session.Restart, 19);
                KeyButton(nextLabel, victory.transform, new Vector2(122, -60), new Vector2(230, 48), next, 19);
            }
            victory.SetActive(false);
        }
    }
}
