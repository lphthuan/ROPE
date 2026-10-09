using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ROPE.Editor
{
    public static class BossNavigationRepair
    {
        [MenuItem("Tools/ROPE/Rebuild Boss Navigation")]
        public static void Rebuild()
        {
            const string scenePath = "Assets/_Game/Scenes/Main/MapBoss.unity";
            const string dataPath = "Assets/_Game/Scenes/Main/MapBoss/NavMesh-Surface_Crustaspikan.asset";
            var originalScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var boss = roots.SelectMany(root => root.GetComponentsInChildren<CrustaspikanMovement>(true))
                    .FirstOrDefault(movement => movement.gameObject.activeInHierarchy);
                if (boss == null)
                {
                    throw new InvalidOperationException("MapBoss has no active Crustaspikan.");
                }
                var agent = boss.GetComponent<NavMeshAgent>();
                var surface = roots.SelectMany(root => root.GetComponentsInChildren<NavMeshSurface>(true))
                    .FirstOrDefault(candidate => candidate.agentTypeID == agent.agentTypeID);
                if (surface == null)
                {
                    surface = new GameObject("Surface_Crustaspikan").AddComponent<NavMeshSurface>();
                    surface.agentTypeID = agent.agentTypeID;
                }
                surface.enabled = true;
                surface.collectObjects = CollectObjects.All;
                surface.BuildNavMesh();
                var generated = surface.navMeshData;
                if (generated == null)
                {
                    throw new InvalidOperationException("The boss NavMesh could not be built.");
                }

                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(agent.transform.position, out var hit, 2f, filter))
                {
                    throw new InvalidOperationException($"The baked boss NavMesh does not cover its spawn at {agent.transform.position}.");
                }

                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(dataPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(generated, dataPath);
                }
                else
                {
                    surface.RemoveData();
                    EditorUtility.CopySerialized(generated, existing);
                    surface.navMeshData = existing;
                    UnityEngine.Object.DestroyImmediate(generated);
                    surface.AddData();
                    EditorUtility.SetDirty(existing);
                }
                EditorUtility.SetDirty(surface);
                AssetDatabase.SaveAssetIfDirty(surface.navMeshData);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("MapBoss could not be saved.");
                }
                Debug.Log($"ROPE boss navigation rebuilt: agent type {agent.agentTypeID}, spawn {agent.transform.position}, NavMesh point {hit.position}.");
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
        }
    }
}
