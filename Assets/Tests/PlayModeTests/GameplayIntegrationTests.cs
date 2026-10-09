using System.Collections;
using System.Linq;
using System.Reflection;
using DatScript;
using NUnit.Framework;
using StarterAssets;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ROPE.Tests.PlayMode
{
    public class GameplayIntegrationTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            var loaded = SceneManager.GetActiveScene();
            var empty = SceneManager.CreateScene("GameplayTestCleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(loaded);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Map1_ArathroxDetectsMovesAndBites()
        {
            yield return LoadGameplay("Map1");
            // Larvae and droids also reuse ArathroxMovement; select by combat role.
            var combat = Object.FindObjectsByType<ArathroxCombat>(FindObjectsSortMode.None)
                .Where(enemy => enemy.GetComponent<DisableShootingOnBreak>() != null)
                .OrderBy(enemy => Vector3.Distance(enemy.transform.position, PlayerHealth.instance.transform.position))
                .FirstOrDefault();
            Assert.That(combat, Is.Not.Null, "Map1 has no active biting Arathrox.");
            var movement = combat.GetComponent<ArathroxMovement>();
            Assert.That(movement, Is.Not.Null, "Map1 has no active Arathrox.");
            yield return VerifyDetectionAndMovement(movement);

            var mouth = Field<Transform>(combat, "_mouthPoint");
            Assert.That(mouth, Is.Not.Null, "Arathrox bite has no mouth transform.");
            movement.GetComponent<BehaviorGraphAgent>().enabled = false;
            movement.Stop();
            var player = PlayerHealth.instance;
            PlacePlayer(player.transform, mouth.position - Vector3.up * 0.8f);
            float before = Field<float>(player, "currentHealth");
            combat.PerformBiteCheck(0.25f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Field<float>(player, "currentHealth"), Is.LessThan(before),
                "The scene's Arathrox bite hitbox did not damage the player.");
        }

        [UnityTest]
        public IEnumerator MapBoss_CrustaspikanDetectsAndMoves()
        {
            yield return LoadGameplay("MapBoss");
            var movement = Object.FindFirstObjectByType<CrustaspikanMovement>();
            Assert.That(movement, Is.Not.Null, "MapBoss has no active Crustaspikan.");
            var combat = movement.GetComponent<CrustaspikanCombat>();
            Assert.That(combat, Is.Not.Null);
            // Between melee and throw ranges, the graph should chase rather than attack in place.
            float chaseDistance = (Field<float>(combat, "_handAttackMaxRange") + Field<float>(combat, "_throwMinRange")) * 0.5f;
            // Its opening roar, unearthed rock and throw can complete before the graph resumes chasing.
            yield return VerifyDetectionAndMovement(movement, chaseDistance, 20f);
        }

        [UnityTest]
        public IEnumerator Map1_LarvaDetectsMovesAndExplodes()
        {
            yield return LoadGameplay("Map1");
            var combat = Object.FindObjectsByType<CrustaspikanLarvaeCombat>(FindObjectsSortMode.None)
                .OrderBy(enemy => Vector3.Distance(enemy.transform.position, PlayerHealth.instance.transform.position))
                .FirstOrDefault();
            Assert.That(combat, Is.Not.Null);
            var movement = combat.GetComponent<ArathroxMovement>();
            Assert.That(movement, Is.Not.Null);
            yield return VerifyDetectionAndMovement(movement);
            movement.GetComponent<BehaviorGraphAgent>().enabled = false;
            movement.Stop();
            var player = PlayerHealth.instance;
            PlacePlayer(player.transform, movement.transform.position + Vector3.right);
            float before = Field<float>(player, "currentHealth");
            combat.TriggerExplosion();
            yield return null;
            Assert.That(Field<float>(player, "currentHealth"), Is.LessThan(before), "The larva explosion did not damage the player.");
        }

        [UnityTest]
        public IEnumerator Map1_DroidDetectsAndFires()
        {
            yield return LoadGameplay("Map1");
            var combat = Object.FindFirstObjectByType<DroidOilCombat>();
            Assert.That(combat, Is.Not.Null);
            Assert.That(Field<GameObject>(combat, "_bulletPrefab"), Is.Not.Null);
            Assert.That(Field<Transform>(combat, "_firePoint"), Is.Not.Null);
            var movement = combat.GetComponent<ArathroxMovement>();
            Assert.That(movement, Is.Not.Null);
            yield return VerifyDetection(movement);
            var player = PlayerHealth.instance;
            float before = Field<float>(player, "currentHealth");
            bool fired = false;
            float deadline = Time.time + 3f;
            while (!fired && Time.time < deadline)
            {
                fired = Object.FindObjectsByType<DroidOilBullet>(FindObjectsSortMode.None).Length > 0
                    || Field<float>(player, "currentHealth") < before;
                yield return null;
            }
            Assert.That(fired, Is.True, "The detected player did not trigger the droid's bullet burst.");
        }

        [UnityTest]
        public IEnumerator MapBoss_SummonedMinionsUseTheirOwnNavigation()
        {
            yield return LoadGameplay("MapBoss");
            StopOtherEnemies(null);
            var combat = Object.FindFirstObjectByType<CrustaspikanCombat>();
            Assert.That(combat, Is.Not.Null);
            var existing = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).ToHashSet();
            int expected = Field<int>(combat, "_minionCount");
            combat.AnimEvent_SpawnMinions();
            yield return null;
            var spawned = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None)
                .Where(agent => !existing.Contains(agent)).ToArray();
            Assert.That(spawned.Length, Is.EqualTo(expected), "The boss did not spawn the configured minions.");
            foreach (var agent in spawned)
            {
                Assert.That(agent.isOnNavMesh, Is.True,
                    $"Summoned {agent.name} has no NavMesh for type {agent.agentTypeID} at {agent.transform.position}.");
            }
        }

        [UnityTest]
        public IEnumerator Map1_PickedUpWeaponFiresAndReloads()
        {
            yield return LoadGameplay("Map1");
            StopOtherEnemies(null);
            var active = PlayerHealth.instance.GetComponent<ActiveWeapon>();
            Assert.That(active, Is.Not.Null);
            var pickup = Object.FindObjectsByType<WeaponPickup>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.weaponPrefab != null && candidate.GetComponent<Collider>() != null);
            Assert.That(pickup, Is.Not.Null, "Map1 has no usable weapon pickup.");
            var trigger = pickup.GetComponent<Collider>();
            Assert.That(trigger.isTrigger, Is.True);
            var player = PlayerHealth.instance.transform;
            PlacePlayer(player, trigger.bounds.center - Vector3.up * 0.8f);
            player.GetComponent<CharacterController>().Move(Vector3.down * 0.01f);
            yield return new WaitForFixedUpdate();
            yield return null;
            var weapon = active.GetCurrentWeapon();
            Assert.That(weapon, Is.Not.Null, "Entering the weapon pickup trigger did not equip its weapon.");
            Assert.That(weapon.ammoConfig, Is.Not.Null);
            Assert.That(weapon.raycastDestination, Is.Not.Null);
            var originalAmmo = weapon.ammoConfig;
            var testAmmo = Object.Instantiate(originalAmmo);
            weapon.ammoConfig = testAmmo;
            try
            {
                testAmmo.currentClipAmmo = 1;
                testAmmo.currentAmmo = 5;
                weapon.StartFiring();
                weapon.UpdateFiring(1f / weapon.fireRate);
                Assert.That(testAmmo.currentClipAmmo, Is.Zero);
                weapon.StopFiring();
                Assert.That(weapon.CanReload(), Is.True);
                weapon.StartReload();
                Assert.That(weapon.isReloading, Is.True);
                weapon.StartFiring();
                Assert.That(weapon.isFiring, Is.False);
                weapon.RefillAmmo();
                Assert.That(weapon.isReloading, Is.False);
                Assert.That(testAmmo.currentClipAmmo, Is.EqualTo(Mathf.Min(testAmmo.clipSize, 5)));
                Assert.That(testAmmo.currentAmmo + testAmmo.currentClipAmmo, Is.EqualTo(5));
                yield return null;
            }
            finally
            {
                if (weapon != null) weapon.ammoConfig = originalAmmo;
                Object.Destroy(testAmmo);
            }
        }

        [UnityTest]
        public IEnumerator Map1_InventoryPicksUpAndThrowsRealSceneItem()
        {
            yield return LoadGameplay("Map1");
            StopOtherEnemies(null);
            var inventory = Object.FindFirstObjectByType<PlayerInventorySystem>();
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.inventorySlots, Has.Length.GreaterThan(0));
            Assert.That(inventory.dropPoint, Is.Not.Null);
            var item = Object.FindObjectsByType<ItemController>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.data != null && candidate.GetComponent<Collider>() != null);
            Assert.That(item, Is.Not.Null, "Map1 has no usable inventory item.");
            var collider = item.GetComponent<Collider>();
            var body = item.GetComponent<Rigidbody>();
            Invoke(inventory, "PickupItem", item, 0);
            yield return null;
            Assert.That(item.transform.parent, Is.EqualTo(inventory.inventorySlots[0]));
            Assert.That(body.isKinematic, Is.True);
            Assert.That(collider.enabled, Is.False);
            Assert.That(inventory.TotalItemCount, Is.EqualTo(1));
            Assert.That(inventory.TotalWeight, Is.EqualTo(item.data.weight).Within(0.001f));
            Assert.That(inventory.TotalValue, Is.EqualTo(item.scrapValue));

            Invoke(inventory, "DropItem", 0, inventory.maxThrowForce);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(item.transform.parent, Is.Null);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(collider.enabled, Is.True);
            Assert.That(inventory.TotalItemCount, Is.Zero);
            Assert.That(body.linearVelocity.sqrMagnitude, Is.GreaterThan(0.01f), "The item received no throw impulse.");
        }

        private static IEnumerator LoadGameplay(string scene)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return new WaitForSeconds(0.5f);
            Assert.That(PlayerHealth.instance, Is.Not.Null);
            // Keep the test target alive while observing actual graph and animation behavior.
            PlayerHealth.instance.maxHealth = 10000f;
            PlayerHealth.instance.ResetHealth();
        }

        private static IEnumerator VerifyDetectionAndMovement(EnemyMovement movement, float? preferredDistance = null, float timeout = 8f)
        {
            // Exercise gameplay while off camera; AI root motion must not depend on renderer visibility.
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            yield return VerifyDetection(movement, preferredDistance);
            var agent = movement.GetComponent<NavMeshAgent>();
            Vector3 start = movement.transform.position;
            float deadline = Time.time + timeout;
            while (movement != null && Vector3.Distance(start, movement.transform.position) < 0.2f && Time.time < deadline)
            {
                yield return null;
            }
            Assert.That(movement, Is.Not.Null, "Enemy disappeared during the AI check.");
            if (Vector3.Distance(start, movement.transform.position) <= 0.2f)
            {
                var animator = movement.GetComponent<Animator>();
                var clips = string.Join(",", animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name));
                Debug.Log($"ROPE AI diagnostic: {movement.name}, start={start}, position={movement.transform.position}, " +
                    $"target={PlayerHealth.instance.transform.position}, vertical={animator.GetFloat("Vertical")}, " +
                    $"turn={animator.GetFloat("Turn")}, moving={animator.GetBool("IsMoving")}, clips={clips}, " +
                    $"path={agent.pathStatus}, stopped={agent.isStopped}, delta={animator.deltaPosition}, " +
                    $"animatorEnabled={animator.enabled}, speed={animator.speed}, normalizedTime={animator.GetCurrentAnimatorStateInfo(0).normalizedTime}.");
            }
            Assert.That(Vector3.Distance(start, movement.transform.position), Is.GreaterThan(0.2f),
                "Behavior graph detected the player but produced no root-motion movement.");
            Assert.That(agent.isOnNavMesh, Is.True);
            Debug.Log($"ROPE AI smoke passed: {movement.name} detected the player and moved {Vector3.Distance(start, movement.transform.position):F2} m.");
        }

        private static IEnumerator VerifyDetection(EnemyMovement movement, float? preferredDistance = null)
        {
            StopOtherEnemies(movement);
            var sensor = movement.GetComponent<VisionSensor>();
            var graph = movement.GetComponent<BehaviorGraphAgent>();
            var agent = movement.GetComponent<NavMeshAgent>();
            Assert.That(sensor, Is.Not.Null);
            Assert.That(graph, Is.Not.Null);
            Assert.That(graph.Graph, Is.Not.Null);
            Assert.That(movement.IsNavigationReady, Is.True);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var player = PlayerHealth.instance.transform;
            float distance = preferredDistance ?? Mathf.Max(5f, sensor.viewRadius * 0.7f);
            bool placed = false;
            for (int index = 0; index < 12 && !placed; index++)
            {
                var direction = Quaternion.Euler(0f, index * 30f, 0f) * movement.transform.forward;
                if (!NavMesh.SamplePosition(movement.transform.position + direction * distance, out var hit, 3f, filter)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(movement.transform.position, hit.position, filter, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;
                PlacePlayer(player, hit.position);
                var eyes = sensor.eyes != null && sensor.eyes.Length > 0 ? sensor.eyes : new[] { movement.transform };
                var center = player.GetComponent<Collider>().bounds.center;
                if (eyes.Where(eye => eye != null).All(eye => Physics.Linecast(eye.position, center,
                        sensor.obstacleMask, QueryTriggerInteraction.Ignore))) continue;
                var facing = player.position - movement.transform.position;
                facing.y = 0f;
                movement.transform.rotation = Quaternion.LookRotation(facing);
                placed = true;
            }
            Assert.That(placed, Is.True, "No reachable visible test point exists near this enemy spawn.");
            yield return new WaitForSeconds(0.5f);
            Assert.That(graph.GetVariable<bool>(sensor.detectedVariableName, out var detected), Is.True);
            Assert.That(detected.Value, Is.True, "The configured sensor never detected a visible player.");
            Assert.That(graph.GetVariable<GameObject>(sensor.playerVariableName, out var target), Is.True);
            Assert.That(target.Value, Is.EqualTo(player.gameObject));

        }

        private static void StopOtherEnemies(EnemyMovement selected)
        {
            foreach (var graph in Object.FindObjectsByType<BehaviorGraphAgent>(FindObjectsSortMode.None))
            {
                if (selected == null || graph.gameObject != selected.gameObject)
                {
                    graph.enabled = false;
                    var movement = graph.GetComponent<EnemyMovement>();
                    if (movement != null) movement.Stop();
                    var agent = graph.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                }
            }
        }

        private static void PlacePlayer(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<ThirdPersonController>();
            if (controller != null) controller.enabled = false;
            var capsule = player.GetComponent<CharacterController>();
            if (capsule != null) capsule.enabled = false;
            player.position = position;
            if (capsule != null) capsule.enabled = true;
            Physics.SyncTransforms();
        }

        private static T Field<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null, $"Cannot read {name} from a missing component.");
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Missing field {name} on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void Invoke(object target, string name, params object[] parameters)
        {
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, parameters);
        }
    }
}
