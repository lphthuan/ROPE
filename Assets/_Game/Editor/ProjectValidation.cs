using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;

namespace ROPE.Editor
{
    public static class ProjectValidation
    {
        public static void ValidateScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No scenes are enabled in Build Settings.");
            }

            var failures = new List<string>();
            var unreadableModels = new HashSet<string>();
            var originalScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var scene in scenes)
                {
                    if (!File.Exists(scene.path))
                    {
                        failures.Add($"Missing build scene: {scene.path}");
                        continue;
                    }

                    var loaded = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                    int eventSystems = loaded.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<EventSystem>())
                        .Count(system => system.isActiveAndEnabled);
                    if (eventSystems > 1)
                    {
                        failures.Add($"{scene.path}: {eventSystems} UI EventSystems are active.");
                    }
                    foreach (var root in loaded.GetRootGameObjects())
                    {
                        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                        {
                            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                            if (missing > 0)
                            {
                                failures.Add($"{scene.path}: {transform.name} has {missing} missing script(s).");
                            }
                        }

                        var outlineRoots = root.GetComponentsInChildren<ItemController>(true)
                            .Select(item => item.transform)
                            .Concat(root.GetComponentsInChildren<Outline>(true).Select(outline => outline.transform))
                            .Distinct();
                        foreach (var outlineRoot in outlineRoots)
                        {
                            var meshes = outlineRoot.GetComponentsInChildren<MeshFilter>(true)
                                .Select(filter => filter.sharedMesh)
                                .Concat(outlineRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                                    .Select(renderer => renderer.sharedMesh));
                            foreach (var mesh in meshes)
                            {
                                if (mesh == null || mesh.isReadable)
                                {
                                    continue;
                                }
                                string path = AssetDatabase.GetAssetPath(mesh);
                                if (unreadableModels.Add(path))
                                {
                                    failures.Add($"Outline requires Read/Write on model: {path} (scene: {scene.path}).");
                                }
                            }
                        }
                        foreach (var agent in root.GetComponentsInChildren<NavMeshAgent>())
                        {
                            if (!agent.isActiveAndEnabled)
                            {
                                continue;
                            }
                            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                            if (!NavMesh.SamplePosition(agent.transform.position, out _, 2f, filter))
                            {
                                failures.Add($"{scene.path}: {agent.name} has no NavMesh for agent type {agent.agentTypeID} near its spawn.");
                            }
                        }
                    }
                    Debug.Log($"ROPE validated scene: {scene.path}");
                }
                string modelReport = Path.GetFullPath(Path.Combine("Logs", "Validation", "UnreadableOutlineModels.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(modelReport));
                File.WriteAllLines(modelReport, unreadableModels.OrderBy(path => path));
            }
            finally
            {
                if (originalScenes.Any(scene => scene.isLoaded && scene.isActive))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
            }
            Debug.Log($"ROPE scene validation passed: {scenes.Length} enabled scenes.");
        }

        public static void CompilePlayerScripts()
        {
            var settings = new ScriptCompilationSettings
            {
                target = BuildTarget.StandaloneWindows64,
                group = BuildTargetGroup.Standalone,
                options = ScriptCompilationOptions.None
            };
            string output = Path.GetFullPath(Path.Combine("Logs", "PlayerScriptAssemblies"));
            Directory.CreateDirectory(output);
            var result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
            if (result.assemblies == null || result.assemblies.Count == 0)
            {
                throw new InvalidOperationException("Windows player script compilation produced no assemblies.");
            }
            Debug.Log($"ROPE Windows player script compilation passed: {result.assemblies.Count} assemblies.");
        }
    }
}
