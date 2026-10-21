using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Collects trigger, overlap and swept head contacts for synchronous end-of-step resolution.</summary>
    public sealed class ContactDetector
    {
        private readonly CircleCollider2D _head;
        private readonly int _hookId;
        private readonly List<Collider2D> _reports = new List<Collider2D>(64);
        private readonly List<ContactCandidate> _candidates = new List<ContactCandidate>(64);
        private readonly List<ContactKey> _ends = new List<ContactKey>(64);
        private readonly Collider2D[] _overlaps = new Collider2D[128];
        private readonly RaycastHit2D[] _sweeps = new RaycastHit2D[128];
        private ContactFilter2D _filter;
        /// <summary>Last batch's raw candidate report count, useful for runtime acceptance.</summary>
        public int LastReportCount { get; private set; }
        /// <summary>Creates a detector using only trigger shapes.</summary>
        public ContactDetector(HookMotor hook) : this(hook.Head) { }
        /// <summary>Creates the same detector for a concrete hook ID and target layer mask.</summary>
        public ContactDetector(CircleCollider2D head, int hookId = 0, int layerMask = -1)
        {
            _head = head; _hookId = hookId;
            _filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = layerMask };
        }
        /// <summary>Updates a reused hook's query mask when a fresh run copies different risk rules.</summary>
        public void SetLayerMask(int layerMask) { _filter.SetLayerMask(layerMask); }
        /// <summary>Clears previous-step callbacks before bodies move.</summary>
        public void Begin() { _reports.Clear(); _candidates.Clear(); _ends.Clear(); }
        /// <summary>Called from physics messages; no model access or capture occurs here.</summary>
        public void Report(Collider2D other) { _reports.Add(other); }
        /// <summary>Queries after Physics2D.Simulate has delivered this step's callbacks.</summary>
        public ContactBatch Finish(RunModel run, long step, Vector2 previous)
        {
            Vector2 current = _head.transform.position;
            float radius = _head.radius * Mathf.Abs(_head.transform.lossyScale.x);
            int count = Physics2D.OverlapCircle(current, radius, _filter, _overlaps);
            for (int i = 0; i < count; i++)
            {
                var fish = Eligible(_overlaps[i]);
                if (fish != null) { _ends.Add(new ContactKey(_hookId, fish.InstanceId)); _reports.Add(_overlaps[i]); }
            }
            Vector2 delta = current - previous;
            if (delta.sqrMagnitude > .000001f)
            {
                count = Physics2D.CircleCast(previous, radius, delta.normalized, _filter, _sweeps, delta.magnitude);
                for (int i = 0; i < count; i++) _reports.Add(_sweeps[i].collider);
            }
            foreach (var collider in _reports)
            {
                var fish = Eligible(collider);
                if (fish == null) continue;
                Vector2 centre = collider.bounds.center;
                float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(centre - previous, delta) / delta.sqrMagnitude) : 0;
                _candidates.Add(new ContactCandidate(_hookId, fish.InstanceId, fish.Definition.speciesId,
                    (centre - (previous + t * delta)).sqrMagnitude));
            }
            LastReportCount = _candidates.Count;
            return new ContactBatch(run.RunId, step, _candidates, _ends);
        }
        private static FishActor Eligible(Collider2D collider)
        {
            if (collider == null || !collider.enabled) return null;
            var fish = collider.GetComponentInParent<FishActor>();
            return fish != null && !fish.IsCaptured && fish.Definition.isCatchable ? fish : null;
        }
    }
}
