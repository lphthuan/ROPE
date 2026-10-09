using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Behavior;
using UnityEditor;

namespace ROPE.Tests
{
    public class BehaviorGraphSerializationTests
    {
        private static IEnumerable<string> GraphPaths()
        {
            return AssetDatabase.FindAssets("t:BehaviorAuthoringGraph",
                    new[] { "Assets/_Game/Data/AI" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path);
        }

        [Test]
        public void EnemyBehaviorGraphs_AreDiscovered()
        {
            Assert.That(GraphPaths(), Is.Not.Empty);
        }

        [TestCaseSource(nameof(GraphPaths))]
        public void EnemyBehaviorGraph_ResolvesSerializedTypes(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Assert.That(assets.OfType<BehaviorGraph>(), Is.Not.Empty,
                $"No runtime behavior graph was loaded from {path}.");

            foreach (var asset in assets)
            {
                Assert.That(asset, Is.Not.Null, $"Missing sub-asset in {path}.");
                Assert.That(SerializationUtility.HasManagedReferencesWithMissingTypes(asset), Is.False,
                    $"Unresolved managed reference in {path} ({asset.name}).");

                using (var serialized = new SerializedObject(asset))
                {
                    var property = serialized.GetIterator();
                    var visitedReferences = new HashSet<long>();
                    bool enterChildren = true;
                    while (property.Next(enterChildren))
                    {
                        enterChildren = true;
                        if (property.propertyType == SerializedPropertyType.ManagedReference)
                        {
                            enterChildren = visitedReferences.Add(property.managedReferenceId);
                            continue;
                        }

                        if (property.propertyType != SerializedPropertyType.String ||
                            (property.name != "m_SerializableType" && property.name != "RuntimeTypeString") ||
                            string.IsNullOrEmpty(property.stringValue))
                        {
                            continue;
                        }

                        Assert.That(Type.GetType(property.stringValue, false), Is.Not.Null,
                            $"Unresolved type at {path}: {property.propertyPath} = {property.stringValue}");
                    }
                }
            }
        }
    }
}
