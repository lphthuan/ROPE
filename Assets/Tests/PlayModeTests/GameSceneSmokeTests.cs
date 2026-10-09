using System.Collections;
using DatScript;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayModeTests
{
    public class GameSceneSmokeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            foreach (var manager in Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Object.Destroy(manager.gameObject);
            }

            var loaded = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("SmokeTestCleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(loaded);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainScenes_StartAndReturnToMenu()
        {
            var scenes = new[] { "MainMenu", "Cinematic", "Map1", "MapBoss", "MainMenu" };
            foreach (string scene in scenes)
            {
                Time.timeScale = 1f;
                if (scene == "Cinematic")
                {
                    MenuManager.Instance.PlayGame();
                    yield return null;
                }
                else
                {
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                }
                yield return new WaitForSecondsRealtime(0.5f);

                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene));
                if (scene == "Cinematic")
                {
                    var cinematic = Object.FindFirstObjectByType<CinematicController>();
                    Assert.That(cinematic, Is.Not.Null);
                    Assert.That(cinematic.videoPlayer, Is.Not.Null);
                    Assert.That(cinematic.videoPlayer.clip, Is.Not.Null);
                    Assert.That(cinematic.nextSceneName, Is.EqualTo("Map1"));
                    continue;
                }
                Assert.That(MenuManager.Instance, Is.Not.Null, $"No menu manager in {scene}.");
                Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1),
                    $"{scene} must have exactly one active UI EventSystem.");
                if (scene != "MainMenu")
                {
                    var player = GameObject.FindGameObjectWithTag("Player");
                    Assert.That(player, Is.Not.Null, $"No player in {scene}.");
                    Assert.That(player.GetComponent<PlayerHealth>(), Is.Not.Null);
                    Assert.That(GameManager.instance, Is.Not.Null, $"No game manager in {scene}.");

                    foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                    {
                        if (agent.isActiveAndEnabled)
                        {
                            Assert.That(agent.isOnNavMesh, Is.True,
                                $"{scene}: {agent.name} (type {agent.agentTypeID}) is outside its NavMesh at {agent.transform.position}.");
                        }
                    }

                    // A manager carried over from the previous map can hold destroyed scene references.
                    var checkpoint = player.transform.position + Vector3.up;
                    GameManager.instance.SetCheckpoint(checkpoint);
                    GameManager.instance.RespawnPlayer();
                    Assert.That(Vector3.Distance(player.transform.position, checkpoint), Is.LessThan(0.01f),
                        $"Respawn did not use the player and checkpoint in {scene}.");
                }
            }
        }
    }
}
