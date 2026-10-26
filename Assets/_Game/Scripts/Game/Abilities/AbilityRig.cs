using System;
using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Targets newly startled by one resolved physics query, attributed to its source pickup.</summary>
    public readonly struct SonarPulseResult
    {
        /// <summary>Run, pickup, pulse index and actual game-clock time.</summary>
        public SonarPulse Pulse { get; }
        /// <summary>Actual query centre in world space.</summary>
        public Vector2 Origin { get; }
        /// <summary>Previously unstartled fish successfully affected by this pulse.</summary>
        public IReadOnlyList<int> FreshFishIds { get; }
        /// <summary>Copies one query's result; the list is never reused by the adapter.</summary>
        public SonarPulseResult(SonarPulse pulse, Vector2 origin, IReadOnlyList<int> freshFishIds)
        { Pulse = pulse; Origin = origin; FreshFishIds = freshFishIds; }
    }
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
        /// <summary>Resolved per-pulse evidence, without mixing concurrent sonar activations.</summary>
        public event Action<SonarPulseResult> SonarResolved;
        /// <summary>Creates scene-owned effects from existing hook art and the shared line material.</summary>
        public AbilityRig(Transform parent, HookMotor main, FishSpawner fish, FishingConfig config, BranchHookConfig branches, Sprite hookArt,
            SurfaceLighting lighting = null)
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
                // Star eligibility is resolved by Core, so normal-size one-star heads must also reach the batch.
                view.Initialize(i + 1, hookArt, main.RopeMaterial, main.Radius * .65f, -1, main.BranchMetalMaterial, lighting); _branches.Add(view);
            }
        }
        /// <summary>Disconnects the previous run, resets views and binds accepted activation events.</summary>
        public void Bind(AbilityController abilities)
        {
            Unbind(); _abilities = abilities; _wave.enabled = false;
            foreach (var branch in _branches)
            { branch.ResetView(); branch.Detector.SetLayerMask(-1); }
            abilities.Sonar.PulseRequested += FireSonar; abilities.Branches.Activated += Deploy;
        }
        /// <summary>Removes only managed subscriptions, safe during scene destruction.</summary>
        public void Unbind()
        {
            if (_abilities == null) return;
            _abilities.Sonar.PulseRequested -= FireSonar; _abilities.Branches.Activated -= Deploy; _abilities = null;
        }
        private void FireSonar(SonarPulse pulse)
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
            var fresh = SonarResolved != null ? new List<int>() : null;
            foreach (var command in _abilities.Sonar.ResolveTargets(targets))
                if (_fish.TryGet(command.FishId, out var fish))
                {
                    if (fresh != null && !fish.IsScared) fresh.Add(fish.InstanceId);
                    fish.Scare(command.Direction, command.Duration, command.SpeedMultiplier);
                }
            if (fresh != null) SonarResolved?.Invoke(new SonarPulseResult(pulse, _waveOrigin, fresh.AsReadOnly()));
        }
        private void Deploy() { foreach (var branch in _branches) branch.Deploy(_main.transform.position); }
        /// <summary>Advances all effects on the gameplay clock before the single Physics2D simulation.</summary>
        public void Tick(RunModel run, float dt)
        {
            if (_wave.enabled)
            { _waveAge += dt; if (_waveAge >= _waveSeconds) _wave.enabled = false; else DrawWave(); }
            float spread = _config.halfWidth + 1.5f;
            spread *= 2 * _branchConfig.radiusScreenFraction;
            if (run.State == RunState.Surfacing || run.State == RunState.Result) return;
            // Reserve real time for folding at the fastest possible ascent; late full-load changes must not jump the range.
            float maximumSpeed = Mathf.Max(run.Rules.AscentSpeed * Mathf.Max(1,run.Rules.FullMultiplier),
                run.Rules.Reeling.Enabled ? run.Rules.Reeling.MaxSpeed : 0);
            float retrievalStart = Mathf.Max(7,maximumSpeed * _config.unitsPerMetre * .7f + 3);
            float retrieval = run.State == RunState.Ascending ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(retrievalStart,3,run.Depth * _config.unitsPerMetre)) : 0;
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i]; if (!branch.gameObject.activeSelf) continue;
                float side = _branches.Count == 1 ? 1 : Mathf.Lerp(-1, 1, (float)i / (_branches.Count - 1));
                var offset = new Vector2(side * spread, .25f);
                if (_abilities.Branches.Model.Active && !_abilities.Branches.Model.IsActive(branch.HookId)) offset = Vector2.zero;
                branch.Tick(_main.transform.position, offset, _config.halfWidth,
                    _branchConfig.deploySeconds, _branchConfig.followSmoothTime,
                    run.State == RunState.Ascending && _abilities.Branches.Model.IsActive(branch.HookId), dt, retrieval);
                if (retrieval > 0)
                {
                    int slot = 0;
                    foreach (var capture in run.Captures)
                    {
                        if (capture.HookId != branch.HookId) continue;
                        if (_fish.TryGetOwned(capture.FishInstanceId,out var held))
                        {
                            var hang=Vector3.Lerp(branch.HangPosition(slot),new Vector3(0,-.22f-slot*.28f,0),retrieval);
                            held.Rehang(slot,hang);
                            var bounds=held.Body.bounds;
                            if(bounds.max.y>-1.8f && bounds.min.y<1.02f)
                                held.Rehang(slot,hang+Vector3.right*Mathf.Clamp(0,-.70f-bounds.min.x,.70f-bounds.max.x));
                        }
                        slot++;
                    }
                }
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
