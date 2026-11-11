using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace TinyWorkshop.Tests
{
    public sealed class SceneIntegrityTests
    {
        [TestCase("01_OneButton")]
        [TestCase("02_Movement")]
        [TestCase("03_Physics")]
        [TestCase("04_MainGame")]
        public void SavedSceneHasRequiredReferencesAndPersistentNavigation(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_TinyWorkshop/Scenes/" + name + ".unity");
            var session = Object.FindFirstObjectByType<WorkshopSession>();
            Assert.NotNull(session);
            Assert.NotNull(session.Hud);
            Assert.NotNull(session.Feedback);
            Assert.NotNull(session.gearPrefab);
            Assert.NotNull(session.worldCamera);
            Assert.NotNull(session.Feedback.sparkSprite);
            Assert.GreaterOrEqual(session.Feedback.clips.Length, 10);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), transform.name);
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    Assert.Greater(button.onClick.GetPersistentEventCount(), 0, button.name);
            }
            if (session.mode == WorkshopMode.MainGame)
            {
                Assert.NotNull(session.Robot);
                Assert.NotNull(session.Machine);
                Assert.NotNull(session.Station);
            }
        }
    }
}
