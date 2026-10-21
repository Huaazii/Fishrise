using System;
using System.Collections;
using System.Linq;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Fishing.Tests
{
    /// <summary>Shared saved-scene fixture for focused ability/Physics2D acceptance.</summary>
    public abstract class AbilityTestScene
    {
        protected GameBootstrap Game;
        protected FishDefinition Small, Silver, Blue;
        [UnitySetUp]
        public IEnumerator Load()
        {
            Resolution(800); SceneManager.LoadScene("Main"); yield return null; yield return null;
            Game = Object.FindObjectOfType<GameBootstrap>(); Assert.That(Game.HasBindings, Is.True); Game.enabled = false;
            Small = Game.Spawner.Spawned.First(f => f.Definition.speciesId == "small").Definition;
            Silver = Game.Spawner.Spawned.First(f => f.Definition.speciesId == "silver").Definition;
            Blue = Game.Spawner.Spawned.First(f => f.Definition.speciesId == "blue").Definition;
            Game.Spawner.Clear();
        }
        [UnityTearDown]
        public IEnumerator Leave()
        {
            Resolution(800); SceneManager.LoadScene("Menu"); yield return null; yield return null; LogAssert.NoUnexpectedReceived();
        }
        protected void Until(RunState state)
        { for (int i = 0; i < 6000 && Game.Run.State != state; i++) Game.AdvanceGameplay(.02f); Assert.That(Game.Run.State, Is.EqualTo(state)); }
        protected void Steps(int count) { for (int i = 0; i < count; i++) Game.AdvanceGameplay(.02f); }
        protected FishActor AtHead(FishDefinition definition, Vector3 centre, float speed = 0, Vector2? range = null)
        {
            var fish = Game.Spawner.SpawnOne(definition, centre, 1, 4.5f, speed, range); Align(fish, centre); return fish;
        }
        protected static void Align(FishActor fish, Vector3 centre)
        { fish.transform.position += centre - fish.Head.transform.TransformPoint(fish.Head.offset); }
        protected static void Click(string name) { GameObject.Find(name).GetComponent<Button>().onClick.Invoke(); }
        protected static Text Label(string name) => GameObject.Find(name).GetComponent<Text>();
        protected static void Resolution(int height)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Fishing.Editor.FishingPreview")).First(t => t != null);
            type.GetMethod("Resolution").Invoke(null, new object[] { 450, height });
        }
    }
}
