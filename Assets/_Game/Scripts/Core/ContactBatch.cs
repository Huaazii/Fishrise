using System;
using System.Collections.Generic;

namespace Fishing.Core
{
    /// <summary>Identity of one hook/fish overlap, distinct from capture deduplication.</summary>
    public readonly struct ContactKey : IEquatable<ContactKey>
    {
        /// <summary>Source hook identity.</summary>
        public int HookId { get; }
        /// <summary>Spawn identity.</summary>
        public int FishId { get; }
        /// <summary>Creates an overlap key.</summary>
        public ContactKey(int hookId, int fishId) { HookId = hookId; FishId = fishId; }
        /// <summary>Tests the identities.</summary>
        public bool Equals(ContactKey other) => HookId == other.HookId && FishId == other.FishId;
        /// <summary>Tests a boxed key.</summary>
        public override bool Equals(object obj) => obj is ContactKey other && Equals(other);
        /// <summary>Hash combining hook and spawn identity.</summary>
        public override int GetHashCode() => unchecked((HookId * 397) ^ FishId);
    }

    /// <summary>One reported contact. Run/step identity is inherited from its enclosing batch.</summary>
    public readonly struct ContactCandidate
    {
        /// <summary>Reporting hook identity.</summary>
        public int HookId { get; }
        /// <summary>Fish spawn identity.</summary>
        public int FishId { get; }
        /// <summary>Species key.</summary>
        public string SpeciesId { get; }
        /// <summary>Squared shortest distance from this step's hook path to the head centre.</summary>
        public float DistanceSquared { get; }
        /// <summary>Creates a pure-data candidate.</summary>
        public ContactCandidate(int hookId, int fishId, string speciesId, float distanceSquared)
        { HookId = hookId; FishId = fishId; SpeciesId = speciesId; DistanceSquared = distanceSquared; }
    }

    /// <summary>Complete immutable detection results from one fixed step.</summary>
    public sealed class ContactBatch
    {
        /// <summary>Run identity used to reject delayed reports after restarting.</summary>
        public Guid RunId { get; }
        /// <summary>Monotonic fixed-step sequence.</summary>
        public long Step { get; }
        /// <summary>Trigger, overlap and swept reports, possibly duplicated.</summary>
        public IReadOnlyList<ContactCandidate> Candidates { get; }
        /// <summary>Contacts still overlapping at the end of the step; a sweep can have no end overlap.</summary>
        public IReadOnlyList<ContactKey> EndContacts { get; }
        /// <summary>Copies detector-owned collections before rule resolution.</summary>
        public ContactBatch(Guid runId, long step, IEnumerable<ContactCandidate> candidates, IEnumerable<ContactKey> endContacts)
        {
            RunId = runId; Step = step;
            Candidates = new List<ContactCandidate>(candidates).AsReadOnly();
            EndContacts = new List<ContactKey>(endContacts).AsReadOnly();
        }
    }
}
