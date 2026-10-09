using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayModeTests
{
    public class EnemyHealthLifecycleTests
    {
        private GameObject enemy;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (enemy != null)
            {
                Object.Destroy(enemy);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestOverkillDamageClampsAtZeroAndDestroysEnemy()
        {
            enemy = new GameObject("Enemy_OverkillTest");
            var health = enemy.AddComponent<EnemyHealth>();
            health.maxHealth = 100;
            health.curentHealth = 60;
            var hitbox = enemy.AddComponent<EnemyHitbox>();
            hitbox.Type = EnemyHitbox.HitboxType.Normal;
            hitbox.MainHealth = health;

            int deathCount = 0;
            health.OnDeath += _ => deathCount++;
            hitbox.TakeDamage(80);
            Assert.That(health.curentHealth, Is.Zero);
            Assert.That(deathCount, Is.EqualTo(1));

            hitbox.TakeDamage(80);
            Assert.That(deathCount, Is.EqualTo(1), "Death must only be signalled once.");
            yield return null;
            Assert.That(enemy == null, Is.True, "A defeated enemy must be destroyed in Play Mode.");
        }
    }
}
