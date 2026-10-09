using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ROPE.Tests.PlayMode
{
    public class MenuInteractionTests
    {
        private InputSettings originalSettings;
        private InputSettings testSettings;
        private InputActionAsset testActions;
        private Mouse mouse;

        [SetUp]
        public void SetUp()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            // Give the test input to the game even when the Editor is behind the MCP client.
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            mouse = InputSystem.AddDevice<Mouse>("MenuTestMouse");
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            var loaded = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("MenuTestCleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(loaded);
            if (testActions != null) Object.Destroy(testActions);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings = originalSettings;
            Object.Destroy(testSettings);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenu_MouseClickPlayLoadsCinematic()
        {
            yield return LoadMainMenu();
            yield return Click(FindButton("PlayButton"));
            float deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name == "MainMenu" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Cinematic"),
                "A mouse click must reach the serialized Play listener and load the cinematic.");
        }

        [UnityTest]
        public IEnumerator MainMenu_MouseClickQuitReachesQuitListener()
        {
            yield return LoadMainMenu();
            var button = FindButton("QuitButton");
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.EqualTo(MenuManager.Instance));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MenuManager.QuitGame)));
            Assert.That(button.onClick.GetPersistentListenerState(0), Is.Not.EqualTo(UnityEventCallState.Off));

            // Observe the UI dispatch without stopping the Test Runner's Play Mode session.
            button.onClick.SetPersistentListenerState(0, UnityEventCallState.Off);
            bool clicked = false;
            button.onClick.AddListener(() => clicked = true);
            yield return Click(button);
            Assert.That(clicked, Is.True, "Quit must receive the mouse click through the real input module.");
        }

        [UnityTest]
        public IEnumerator MenuStartup_UnlocksCursorAndClearsPreviousPause()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("MenuStartup");
            SceneManager.SetActiveScene(scene);
            yield return SceneManager.UnloadSceneAsync(previous);
            var manager = new GameObject("MenuManager").AddComponent<MenuManager>();
            manager.gameSceneNames = new[] { "Map1", "MapBoss" };
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
        }

        private IEnumerator LoadMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            var system = EventSystem.current;
            Assert.That(system, Is.Not.Null);
            var module = system.GetComponent<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.isActiveAndEnabled, Is.True);
            Assert.That(module.actionsAsset, Is.Not.Null);
            testActions = Object.Instantiate(module.actionsAsset);
            // Physical mouse/touch input must not interfere with this test's pointer.
            testActions.devices = new InputDevice[] { mouse };
            module.actionsAsset = testActions;
            yield return null;
        }

        private static Button FindButton(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(candidate => candidate.name == name);
            Assert.That(button.IsInteractable(), Is.True, name);
            return button;
        }

        private IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));

            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }
    }
}
