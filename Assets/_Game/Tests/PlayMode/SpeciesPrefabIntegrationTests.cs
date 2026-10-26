using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Fishing.Tests
{
    /// <summary>Checks baked species assets and reuse after capture, scare and opposite-facing respawns.</summary>
    public sealed class SpeciesPrefabIntegrationTests : AbilityTestScene
    {
        /// <summary>Verifies each saved prefab has the matching art, head shape, layer and mouth anchor.</summary>
        [UnityTest]
        public IEnumerator SevenIndependentPrefabsHaveSerializedArtHeadAndMouth()
        {
            var definitions = Resources.FindObjectsOfTypeAll<FishDefinition>().OrderBy(f => f.speciesId).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(7));
            Assert.That(definitions.Select(f => f.prefab).Distinct().Count(), Is.EqualTo(7));
            foreach (var definition in definitions)
            {
                var prefab = definition.prefab;
                Assert.That(prefab, Is.Not.Null, definition.speciesId);
                Assert.That(prefab.Definition, Is.SameAs(definition));
                Assert.That(prefab.transform.localScale, Is.EqualTo(new Vector3(definition.size, definition.size, 1)));
                Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>().Single().sprite, Is.SameAs(definition.sprite));
                Assert.That(prefab.GetComponentsInChildren<Collider2D>().Single(), Is.SameAs(prefab.Head));
                Assert.That(prefab.Head.isTrigger, Is.True); Assert.That(prefab.Head.enabled, Is.EqualTo(definition.isCatchable));
                Assert.That(prefab.Head.radius, Is.GreaterThan(0), definition.speciesId + " needs a positive authored circle");
                Assert.That(float.IsInfinity(prefab.Head.radius), Is.False);
                Assert.That(prefab.Head.gameObject.layer, Is.EqualTo(definition.isSmall ? LayerMask.NameToLayer(FishActor.SmallFishLayer) : 0));
                Assert.That((Vector2)prefab.Mouth.localPosition, Is.EqualTo(definition.mouthPosition));
#if UNITY_EDITOR
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo("Assets/_Game/Prefabs/Fish_" + definition.speciesId + ".prefab"));
                Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Regular));
#endif
            }
#if UNITY_EDITOR
            Assert.That(AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Prefabs/Fish.prefab"), Is.Null);
#endif
            yield return null;
        }

        /// <summary>Verifies species identity, head recovery and mouth attachment across twenty reuse cycles.</summary>
        [UnityTest]
        public IEnumerator SpeciesPoolsKeepTheirPrefabAndResetCapturedOrScaredStateForTwentyRuns()
        {
            var definitions = Resources.FindObjectsOfTypeAll<FishDefinition>().OrderBy(f => f.speciesId).ToArray();
            var owner = new GameObject("Species pool acceptance"); owner.transform.SetParent(Game.transform);
            var pool = new FishSpawner(owner.transform); var identities = new Dictionary<FishDefinition, int>();
            try
            {
                foreach (var definition in definitions)
                {
                    var fish = pool.SpawnOne(definition, Vector2.zero, 1, 4.5f);
                    identities.Add(definition, fish.GetInstanceID());
                    if (definition.isSmall) fish.Scare(-1, 2.5f, 3);
                    else fish.Attach(Game.Hook.transform, 0, Vector3.zero);
                }
                for (int run = 0; run < 20; run++)
                {
                    pool.Clear();
                    foreach (var definition in definitions.Reverse())
                    {
                        float direction = run % 2 == 0 ? -1 : 1;
                        var fish = pool.SpawnOne(definition, new Vector2(0, -10), direction, 4.5f);
                        Assert.That(fish.GetInstanceID(), Is.EqualTo(identities[definition]));
                        Assert.That(fish.Definition, Is.SameAs(definition));
                        Assert.That(fish.IsCaptured || fish.IsScared, Is.False); Assert.That(fish.Head.enabled, Is.True);
                        Assert.That(fish.transform.localRotation, Is.EqualTo(Quaternion.identity));
                        AssertAuthoredHead(fish);
                        float authoredX = HeadCentre(definition.prefab).x - definition.prefab.transform.position.x;
                        Assert.That(HeadCentre(fish).x, Is.EqualTo(authoredX * direction).Within(.0001));
                        fish.Attach(Game.Hook.transform, 1, new Vector3(.2f, -.1f, 0));
                        Assert.That(Vector3.Distance(fish.Mouth.position, Game.Hook.transform.TransformPoint(new Vector3(.2f, -.1f, 0))), Is.LessThan(.0001f));
                    }
                    Assert.That(pool.AllocatedCount, Is.EqualTo(7)); Assert.That(pool.ActiveCount, Is.EqualTo(7));
                }
            }
            finally { pool.Clear(); Object.Destroy(owner); }
            yield return null;
        }

        /// <summary>Checks both facings against actual physics at the inside and outside of every authored circle.</summary>
        [UnityTest]
        public IEnumerator AuthoredCirclesRemainInUseAndMatchPhysicsAtTheirBoundaries()
        {
            var definitions = Resources.FindObjectsOfTypeAll<FishDefinition>().OrderBy(f => f.speciesId).ToArray();
            var filter = new ContactFilter2D { useTriggers = true }; var hits = new Collider2D[32];
            foreach (var definition in definitions) foreach (float direction in new[] { -1f, 1f })
            {
                Game.Spawner.Clear();
                var fish = Game.Spawner.SpawnOne(definition, new Vector2(0,-10), direction, 4.5f, 0);
                fish.ApplyDefinition(); AssertAuthoredHead(fish); Physics2D.SyncTransforms();
                var centre = HeadCentre(fish); float radius = fish.Head.bounds.extents.x;
                Assert.That(Vector3.Distance(centre,fish.Head.bounds.center), Is.LessThan(.0001f));
                int inside = Physics2D.OverlapCircle(centre + Vector3.right*(radius*.9f),.001f,filter,hits);
                Assert.That(hits.Take(inside), Has.Member(fish.Head), definition.speciesId + " authored interior missed");
                int outside = Physics2D.OverlapCircle(centre + Vector3.right*(radius*1.1f+.002f),.001f,filter,hits);
                Assert.That(hits.Take(outside), Has.No.Member(fish.Head), definition.speciesId + " authored exterior hit");
            }
            yield return null;
        }

        private static void AssertAuthoredHead(FishActor fish)
        {
            var expected = fish.Definition.prefab.Head; var actual = fish.Head;
            Assert.That(actual.radius, Is.EqualTo(expected.radius));
            Assert.That(actual.offset, Is.EqualTo(expected.offset));
            Assert.That(actual.transform.localPosition, Is.EqualTo(expected.transform.localPosition));
            Assert.That(actual.transform.localRotation, Is.EqualTo(expected.transform.localRotation));
            Assert.That(actual.transform.localScale, Is.EqualTo(expected.transform.localScale));
        }
    }
}
