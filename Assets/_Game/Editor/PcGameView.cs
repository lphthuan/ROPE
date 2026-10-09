using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ROPE.Editor
{
    [InitializeOnLoad]
    public static class PcGameView
    {
        private const string SimulatorWindowType = "UnityEditor.DeviceSimulation.SimulatorWindow";

        static PcGameView()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += PrepareAfterReload;
        }

        private static bool IsPcTarget =>
            BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget) == BuildTargetGroup.Standalone;

        private static void PrepareAfterReload()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                Resources.FindObjectsOfTypeAll<EditorWindow>()
                    .Any(window => window.GetType().FullName == SimulatorWindowType))
            {
                Prepare();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Prepare();
        }

        [MenuItem("Tools/ROPE/Use PC Game View")]
        public static void Prepare()
        {
            if (Application.isBatchMode || !IsPcTarget) return;

            // The Simulator's Input System plugin disables native mouse/pen devices
            // for its entire lifetime, even when its simulation is inactive.
            var simulators = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => window.GetType().FullName == SimulatorWindowType)
                .ToArray();
            foreach (var simulator in simulators) simulator.Close();

            // GameView is an internal Unity type; use the public EditorWindow API
            // and reflect only the Unity 6 focus-on-play setting.
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            var gameView = EditorWindow.GetWindow(gameViewType);
            var behavior = gameViewType.GetProperty("enterPlayModeBehavior",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (behavior != null && behavior.CanWrite && behavior.PropertyType.IsEnum)
            {
                behavior.SetValue(gameView, Enum.Parse(behavior.PropertyType, "PlayFocused"));
            }
            gameView.Show();
            gameView.Focus();

            if (simulators.Length > 0)
            {
                Debug.Log("ROPE: closed Device Simulator and restored PC Game View input.");
            }
        }
    }
}
