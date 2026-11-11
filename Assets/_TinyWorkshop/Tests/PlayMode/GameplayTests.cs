using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TinyWorkshop.Tests
{
    public sealed class GameplayTests
    {
        Keyboard keyboard;
        WorkshopSession session;
        float jumpHeight;
        IEnumerator Open(string name)
        {
            yield return SceneManager.LoadSceneAsync(name);
            yield return new WaitForSeconds(.15f);
            session = Object.FindFirstObjectByType<WorkshopSession>();
            Assert.NotNull(session);
            System.IO.Directory.CreateDirectory("Captures");
            ScreenCapture.CaptureScreenshot("Captures/" + name + ".png");
            yield return null;
        }
        void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }
        InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
        InputSettings.BackgroundBehavior savedBackground;
        [SetUp]
        public void Setup()
        {
            // Simulated keys must reach the game even when the Editor/Game view is not focused
            // (e.g. tests started remotely); otherwise input tests fail randomly.
            savedEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
        }
        [TearDown]
        public void Teardown()
        {
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
            InputSystem.settings.backgroundBehavior = savedBackground;
        }
        [UnityTest]
        public IEnumerator MainGameCompletesThroughRealProductionPickupAndDelivery()
        {
            yield return Open("04_MainGame");
            Assert.AreEqual("Locked", session.QuestState);
            Assert.IsFalse(session.Machine.TryPress());
            session.Interact();
            Assert.AreEqual("Active", session.QuestState);
            var body = session.Robot.GetComponent<Rigidbody2D>();
            body.position = new Vector2(-6.8f, -2.8f);
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 9; i++)
            {
                Assert.IsTrue(session.Machine.TryPress(), "Press " + i);
                yield return new WaitForSeconds(.15f);
            }
            Assert.AreEqual(3, session.Produced);
            Assert.AreEqual(3, session.WorldGearCount);
            Assert.IsFalse(session.Machine.TryPress());
            var gears = Object.FindObjectsByType<Gear>(FindObjectsSortMode.None);
            Assert.AreEqual(3, gears.Length);
            int savedId = gears[0].Id;
            gears[0].transform.position = new Vector2(0, -7);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(savedId, gears[0].Id);
            Assert.Greater(gears[0].transform.position.y, -5);
            Assert.AreEqual(3, session.Produced);
            for (int i = 0; i < gears.Length; i++)
            {
                if (!gears[i])
                    continue; // Nearby gears can be collected together by the real trigger.
                body.position = gears[i].transform.position;
                body.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(.12f);
                Assert.GreaterOrEqual(session.Carried, i + 1, "Real pickup trigger " + i);
            }
            Assert.AreEqual(3, session.Collected);
            Assert.AreEqual(0, session.WorldGearCount);
            body.position = new Vector2(6.9f, -2.8f);
            yield return new WaitForFixedUpdate();
            session.Interact();
            Assert.AreEqual("Completed", session.QuestState);
            Assert.AreEqual(3, session.Delivered);
            Assert.AreEqual(0, session.Carried);
            Assert.IsFalse(session.RewardClaimed);
            session.Interact();
            Assert.IsTrue(session.RewardClaimed);
            session.Interact();
            Assert.AreEqual("RewardClaimed", session.QuestState);
            yield return new WaitForSeconds(1);
            Assert.Greater(session.lampGlows[0].color.a, .1f);
            Assert.IsFalse(session.Hud.hint.transform.parent.gameObject.activeSelf, "Gameplay hint must not overlap completion panel");
            Assert.IsFalse(session.Hud.toast.gameObject.activeSelf, "Delivery toast must not overlap completion panel");
            ScreenCapture.CaptureScreenshot("Captures/04_MainGame_Complete.png");
            yield return null;
        }
        [UnityTest]
        public IEnumerator MainGameGearsLandOnPlatformsAndStay()
        {
            yield return Open("04_MainGame");
            session.Interact();
            var body = session.Robot.GetComponent<Rigidbody2D>();
            body.position = new Vector2(-6.8f, -2.8f);
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < 9; i++)
            {
                Assert.IsTrue(session.Machine.TryPress(), "Press " + i);
                yield return new WaitForSeconds(.15f);
            }
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(3, session.WorldGearCount, "Gears must not be magnetised from the machine");
            int onPlatforms = 0;
            string trace = "";
            foreach (var gear in Object.FindObjectsByType<Gear>(FindObjectsSortMode.None))
            {
                trace += " " + gear.transform.position;
                if (gear.transform.position.y > -2.6f)
                    onPlatforms++;
            }
            Assert.GreaterOrEqual(onPlatforms, 2, "Most gears should rest on platforms so jumping matters:" + trace);
            ScreenCapture.CaptureScreenshot("Captures/04_MainGame_Gears.png");
            yield return null;
        }
        [UnityTest]
        public IEnumerator OneButtonRequiresNinePressesAndCompletes()
        {
            yield return Open("01_OneButton");
            for (int i = 0; i < 9; i++)
            {
                Keys(Key.Space);
                yield return null;
                Keys();
                yield return new WaitForSeconds(.16f);
            }
            Assert.AreEqual(3, session.Produced);
            Assert.IsTrue(session.DemoComplete);
        }
        [UnityTest]
        public IEnumerator MovementAcceleratesBrakesAndHasVariableJump()
        {
            yield return Open("02_Movement");
            for (float t = 0; t < .25f; t += Time.deltaTime)
            {
                Keys(Key.D);
                yield return null;
            }
            Assert.Greater(session.Robot.Velocity.x, 4, $"D={keyboard.dKey.isPressed}, position={session.Robot.transform.position}");
            Keys();
            yield return new WaitForSeconds(.2f);
            Assert.Less(Mathf.Abs(session.Robot.Velocity.x), .1f);
            yield return Jump(false);
            float high = jumpHeight;
            yield return Jump(true);
            Assert.Greater(high, jumpHeight + .25f, "Holding jump must reach higher");
            session.SelectSecond();
            Assert.AreEqual(4.5f, session.Robot.Settings.maxSpeed);
            session.SelectThird();
            Assert.AreEqual(6.2f, session.Robot.Settings.maxSpeed);
        }
        IEnumerator Jump(bool shortJump)
        {
            Keys();
            var body = session.Robot.GetComponent<Rigidbody2D>();
            body.position = new Vector2(-8, -3.35f);
            body.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(.15f);
            jumpHeight = -10;
            Keys(Key.Space);
            for (float t = 0; t < .6f; t += Time.deltaTime)
            {
                if (shortJump && t > .055f)
                    Keys();
                jumpHeight = Mathf.Max(jumpHeight, body.position.y);
                yield return null;
            }
            Keys();
            yield return new WaitForSeconds(.2f);
        }
        [UnityTest]
        public IEnumerator CoyoteTimeAcceptsJumpAfterLeavingLedge()
        {
            yield return Open("02_Movement");
            var body = session.Robot.GetComponent<Rigidbody2D>();
            body.position = new Vector2(-5.08f, -3.35f);
            body.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(.04f);
            Assert.IsFalse(session.Robot.Grounded);
            Keys(Key.Space);
            yield return new WaitForSeconds(.04f);
            Assert.Greater(body.linearVelocity.y, 5);
        }
        [UnityTest]
        public IEnumerator JumpBufferAcceptsPressBeforeLanding()
        {
            yield return Open("02_Movement");
            var body = session.Robot.GetComponent<Rigidbody2D>();
            body.position = new Vector2(-8, -2.85f);
            body.linearVelocity = new Vector2(0, -5);
            yield return new WaitForFixedUpdate();
            Keys(Key.Space);
            yield return new WaitForSeconds(.12f);
            Assert.Greater(body.linearVelocity.y, 2);
        }
        [UnityTest]
        public IEnumerator PhysicsNaturalModeCanHitAllThreeTargets()
        {
            yield return PhysicsTargets(false);
        }
        [UnityTest]
        public IEnumerator PhysicsDesignedModeCanHitAllThreeTargets()
        {
            yield return PhysicsTargets(true);
        }
        IEnumerator PhysicsTargets(bool designed)
        {
            yield return Open("03_Physics");
            if (designed)
                session.TogglePhysics();
            Assert.AreEqual(designed, session.DesignedPhysics);
            for (int i = 0; i < 30 && !session.DemoComplete; i++)
            {
                session.Launch();
                yield return new WaitForSeconds(.4f);
            }
            yield return new WaitForSeconds(2);
            Assert.AreEqual(3, session.HitTargets, (designed ? "Designed" : "Natural") + " trajectory must reach physical targets");
        }
        [UnityTest]
        public IEnumerator FallingRobotReturnsToSafePosition()
        {
            yield return Open("02_Movement");
            var body = session.Robot.GetComponent<Rigidbody2D>();
            body.position = new Vector2(0, -7);
            yield return new WaitForSeconds(.1f);
            Assert.Greater(body.position.y, -4);
            Assert.Less(body.position.x, -5);
        }
        [UnityTest]
        public IEnumerator SafePositionTracksLastPlatformDuringMovement()
        {
            yield return Open("02_Movement");
            var body = session.Robot.GetComponent<Rigidbody2D>();
            body.position = new Vector2(-3.6f, -2.22f);
            body.linearVelocity = new Vector2(4, 0);
            for (float t = 0; t < .08f; t += Time.deltaTime)
            {
                Keys(Key.D);
                yield return null;
            }
            body.position = new Vector2(0, -7);
            Keys();
            yield return new WaitForSeconds(.12f);
            Assert.Greater(body.position.x, -5);
            Assert.Less(body.position.x, -2);
            Assert.Greater(body.position.y, -2.5f);
        }
        [UnityTest]
        public IEnumerator MovementCourseCompletesWithNormalProfile()
        {
            yield return TraverseCourse(0);
        }
        [UnityTest]
        public IEnumerator MovementCourseCompletesWithHeavyProfile()
        {
            yield return TraverseCourse(1);
        }
        [UnityTest]
        public IEnumerator MovementCourseCompletesWithLightProfile()
        {
            yield return TraverseCourse(2);
        }
        IEnumerator TraverseCourse(int profile)
        {
            yield return Open("02_Movement");
            session.SelectProfile(profile);
            float[] takeoffs = { -5.85f, -2.85f, .25f, 3.35f };
            int jump = 0;
            bool held = false;
            bool leftGround = false;
            string trace = "";
            bool previouslyGrounded = true;
            for (float t = 0; t < 12 && !session.DemoComplete; t += Time.deltaTime)
            {
                var robot = session.Robot;
                if (robot.Grounded && !previouslyGrounded)
                    trace += " land " + robot.transform.position;
                previouslyGrounded = robot.Grounded;
                if (held && !robot.Grounded)
                    leftGround = true;
                if (leftGround && robot.Velocity.y <= 0)
                {
                    held = false;
                    leftGround = false;
                }
                if (!held && robot.Grounded && jump < takeoffs.Length && robot.transform.position.x >= takeoffs[jump])
                {
                    held = true;
                    jump++;
                    trace += " jump " + jump + " " + robot.transform.position;
                }
                if (held)
                    Keys(Key.D, Key.Space);
                else
                    Keys(Key.D);
                yield return null;
            }
            Keys();
            Assert.IsTrue(session.DemoComplete, $"Profile {profile}, jumps {jump}, position {session.Robot.transform.position}; {trace}");
        }
    }
}
