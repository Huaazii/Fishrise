using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Physics/visual adapter for sonar and pooled branch hooks; never owns capture counts.</summary>
    public sealed class AbilityRig
    {
        private readonly HookMotor _main;
        private readonly FishSpawner _fish;
        private readonly FishingConfig _config;
        private readonly BranchHookConfig _branchConfig;
        private readonly List<BranchHookView> _branches = new List<BranchHookView>();
        private readonly List<ContactCandidate> _candidates = new List<ContactCandidate>();
        private readonly List<ContactKey> _ends = new List<ContactKey>();
        private readonly Collider2D[] _query = new Collider2D[64];
        private readonly LineRenderer _wave;
        private readonly int _smallMask;
        private AbilityController _abilities;
        private float _waveAge, _waveSeconds, _waveRadius;
        private Vector3 _waveOrigin;
        /// <summary>Reusable branch views, including inactive pool entries.</summary>
        public IReadOnlyList<BranchHookView> Branches => _branches;
        /// <summary>Sonar line renderer for runtime presentation checks.</summary>
        public LineRenderer Wave => _wave;
        /// <summary>Creates scene-owned effects from existing hook art and the shared line material.</summary>
        public AbilityRig(Transform parent, HookMotor main, FishSpawner fish, FishingConfig config, BranchHookConfig branches, Sprite hookArt)
        {
            _main = main; _fish = fish; _config = config; _branchConfig = branches;
            _smallMask = 1 << LayerMask.NameToLayer(FishActor.SmallFishLayer);
            var wave = new GameObject("Sonar wave"); wave.transform.SetParent(parent, false);
            _wave = wave.AddComponent<LineRenderer>(); _wave.sharedMaterial = main.LineMaterial;
            _wave.loop = true; _wave.positionCount = 64; _wave.startWidth = _wave.endWidth = .045f;
            _wave.sortingOrder = 7; _wave.enabled = false;
            for (int i = 0; i < branches.hookCount; i++)
            {
                var go = new GameObject("Branch Hook " + (i + 1)); go.transform.SetParent(parent, false);
                var view = go.AddComponent<BranchHookView>();
                view.Initialize(i + 1, hookArt, main.LineMaterial, main.Radius * .65f, branches.breakOnLarge ? -1 : _smallMask); _branches.Add(view);
            }
        }
        /// <summary>Disconnects the previous run, resets views and binds accepted activation events.</summary>
        public void Bind(AbilityController abilities)
        {
            Unbind(); _abilities = abilities; _wave.enabled = false;
            foreach (var branch in _branches)
            { branch.ResetView(); branch.Detector.SetLayerMask(abilities.Branches.Effect.BreakOnLarge ? -1 : _smallMask); }
            abilities.Sonar.Activated += FireSonar; abilities.Branches.Activated += Deploy;
        }
        /// <summary>Removes only managed subscriptions, safe during scene destruction.</summary>
        public void Unbind()
        {
            if (_abilities == null) return;
            _abilities.Sonar.Activated -= FireSonar; _abilities.Branches.Activated -= Deploy; _abilities = null;
        }
        private void FireSonar()
        {
            var rules = _abilities.Sonar.Effect; _waveOrigin = _main.transform.position;
            _waveAge = 0; _waveSeconds = rules.WaveSeconds; _waveRadius = rules.Radius * _config.unitsPerMetre;
            _wave.enabled = true; DrawWave(); Physics2D.SyncTransforms();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = _smallMask };
            int count = Physics2D.OverlapCircle(_waveOrigin, _waveRadius, filter, _query);
            var targets = new List<SonarTarget>(count);
            for (int i = 0; i < count; i++)
            {
                var fish = _query[i].GetComponentInParent<FishActor>();
                if (fish == null || fish.IsCaptured) continue;
                Vector2 offset = (Vector2)_query[i].bounds.center - (Vector2)_waveOrigin;
                targets.Add(new SonarTarget(fish.InstanceId, fish.Definition.speciesId,
                    offset.sqrMagnitude / (_config.unitsPerMetre * _config.unitsPerMetre), offset.x));
            }
            foreach (var command in _abilities.Sonar.ResolveTargets(targets))
                if (_fish.TryGet(command.FishId, out var fish)) fish.Scare(command.Direction, command.Duration, command.SpeedMultiplier);
        }
        private void Deploy() { foreach (var branch in _branches) branch.Deploy(_main.transform.position); }
        /// <summary>Advances all effects on the gameplay clock before the single Physics2D simulation.</summary>
        public void Tick(RunModel run, float dt)
        {
            if (_wave.enabled)
            { _waveAge += dt; if (_waveAge >= _waveSeconds) _wave.enabled = false; else DrawWave(); }
            float spread = _config.halfWidth + 1.5f;
            spread *= 2 * _branchConfig.radiusScreenFraction;
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i]; if (!branch.gameObject.activeSelf) continue;
                float side = _branches.Count == 1 ? 1 : Mathf.Lerp(-1, 1, (float)i / (_branches.Count - 1));
                var offset = new Vector2(side * spread, .25f);
                if (_abilities.Branches.Model.Active && !_abilities.Branches.Model.IsActive(branch.HookId)) offset = Vector2.zero;
                branch.Tick(_main.transform.position, offset, _config.halfWidth,
                    _branchConfig.deploySeconds, _branchConfig.followSmoothTime,
                    run.State == RunState.Ascending && _abilities.Branches.Model.IsActive(branch.HookId), dt);
            }
        }
        private void DrawWave()
        {
            float t = Mathf.Clamp01(_waveAge / _waveSeconds), radius = Mathf.Max(.03f, _waveRadius * t);
            _wave.startColor = _wave.endColor = new Color(.25f, 1, 1, 1 - t);
            for (int i = 0; i < 64; i++)
            { float angle = i * Mathf.PI * 2 / 64; _wave.SetPosition(i, _waveOrigin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius); }
        }
        /// <summary>Merges every hook into one physical-step batch, including each branch's swept path.</summary>
        public ContactBatch Combine(ContactBatch main, RunModel run)
        {
            if (!_abilities.Branches.Model.Active) return main;
            _candidates.Clear(); _ends.Clear(); _candidates.AddRange(main.Candidates); _ends.AddRange(main.EndContacts);
            foreach (var branch in _branches)
            {
                if (!_abilities.Branches.Model.IsActive(branch.HookId)) continue;
                var batch = branch.Detector.Finish(run, main.Step, branch.Previous);
                _candidates.AddRange(batch.Candidates); _ends.AddRange(batch.EndContacts);
            }
            return new ContactBatch(main.RunId, main.Step, _candidates, _ends);
        }
        /// <summary>Gets the view for an authoritative branch source ID.</summary>
        public BranchHookView ForHook(int hookId) => _branches[hookId - 1];
    }
}
