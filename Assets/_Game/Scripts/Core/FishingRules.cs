using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fishing.Core
{
    /// <summary>Immutable per-run numeric rules, independent of Unity assets.</summary>
    public sealed class FishingRules
    {
        /// <summary>Maximum reachable depth in metres.</summary>
        public float MaxDepth { get; }
        /// <summary>Depth at which protection stops applying.</summary>
        public float BottomZoneDepth { get; }
        /// <summary>Descent speed in metres per second.</summary>
        public float DiveSpeed { get; }
        /// <summary>Distance over which descent slows down.</summary>
        public float SlowdownDistance { get; }
        /// <summary>Normal ascent speed.</summary>
        public float AscentSpeed { get; }
        /// <summary>Optional interactive ascent rules; disabled for explicit legacy comparisons.</summary>
        public ReelingRules Reeling { get; }
        /// <summary>Ascent multiplier when the main hook is full.</summary>
        public float FullMultiplier { get; }
        /// <summary>Bottom waiting time.</summary>
        public float BottomHoldSeconds { get; }
        /// <summary>Time to ease from rest into retrieval after reaching maximum depth; early returns keep their speed.</summary>
        public float AscentStartSeconds { get; }
        /// <summary>Countdown duration.</summary>
        public float CountdownSeconds { get; }
        /// <summary>Presentation timeout; capture records remain unchanged during this phase.</summary>
        public float SurfacingSeconds { get; }
        /// <summary>Main hook capacity, excluding branches.</summary>
        public int MainCapacity { get; }
        /// <summary>Initial protection charges.</summary>
        public int ProtectionCharges { get; }
        /// <summary>Protection invulnerability duration.</summary>
        public float InvulnerabilitySeconds { get; }
        /// <summary>Copies a validated numeric snapshot.</summary>
        public FishingRules(float maxDepth = 100, float bottomZoneDepth = 96,
            float diveSpeed = 6, float slowdownDistance = 5, float ascentSpeed = 6.5f,
            float fullMultiplier = 5, float bottomHoldSeconds = .35f,
            float countdownSeconds = 3, float surfacingSeconds = .6f,
            int mainCapacity = 10, int protectionCharges = 2, float invulnerabilitySeconds = 1, ReelingRules reeling = null,
            float ascentStartSeconds = .35f)
        {
            if (maxDepth <= 0 || bottomZoneDepth < 0 || bottomZoneDepth > maxDepth ||
                diveSpeed <= 0 || ascentSpeed <= 0 || fullMultiplier <= 0 || mainCapacity < 1 ||
                protectionCharges < 0 || bottomHoldSeconds < 0 || countdownSeconds < 0 ||
                surfacingSeconds < 0 || invulnerabilitySeconds < 0 || slowdownDistance < 0 ||
                ascentStartSeconds < 0 || float.IsNaN(ascentStartSeconds) || float.IsInfinity(ascentStartSeconds))
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "Invalid fishing rule values.");
            MaxDepth = maxDepth; BottomZoneDepth = bottomZoneDepth; DiveSpeed = diveSpeed;
            SlowdownDistance = slowdownDistance; AscentSpeed = ascentSpeed;
            FullMultiplier = fullMultiplier; BottomHoldSeconds = bottomHoldSeconds;
            AscentStartSeconds = ascentStartSeconds;
            CountdownSeconds = countdownSeconds; SurfacingSeconds = surfacingSeconds;
            MainCapacity = mainCapacity; ProtectionCharges = protectionCharges;
            InvulnerabilitySeconds = invulnerabilitySeconds;
            Reeling = reeling ?? new ReelingRules();
        }
    }

    /// <summary>Immutable fish identity, eligibility and reward snapshot.</summary>
    public sealed class FishRules
    {
        /// <summary>Stable species key.</summary>
        public string Id { get; }
        /// <summary>Display name without a scene reference.</summary>
        public string Name { get; }
        /// <summary>Gold per captured fish.</summary>
        public int Gold { get; }
        /// <summary>Shells per captured fish.</summary>
        public int Shells { get; }
        /// <summary>Whether this species is eligible for capture.</summary>
        public bool IsCatchable { get; }
        /// <summary>Small-fish classification used by sonar, independent of branch star eligibility.</summary>
        public bool IsSmall { get; }
        /// <summary>Star rating copied at run start; branches accept one-star catchable fish.</summary>
        public int Rarity { get; }
        /// <summary>Creates a species snapshot.</summary>
        public FishRules(string id, string name, int gold, int shells, bool isSmall = false, bool isCatchable = true, int rarity = 1)
        {
            if (string.IsNullOrWhiteSpace(id) || gold < 0 || shells < 0 || rarity < 1 || rarity > 5)
                throw new ArgumentException("Invalid fish definition.");
            Id = id; Name = name; Gold = gold; Shells = shells;
            IsSmall = isSmall; IsCatchable = isCatchable; Rarity = rarity;
        }
    }

    /// <summary>Flow states; pause is an overlay flag rather than a transition.</summary>
    public enum RunState { Menu, Countdown, Diving, BottomHold, Ascending, Surfacing, Result }
    /// <summary>Capture source category independent of the concrete hook identity.</summary>
    public enum CaptureSource { Main, Branch }

    /// <summary>A captured fish with immutable reward and source data.</summary>
    public sealed class CaptureRecord
    {
        /// <summary>Species key.</summary>
        public string SpeciesId { get; }
        /// <summary>Unique identity assigned on each spawn within a run.</summary>
        public int FishInstanceId { get; }
        /// <summary>Main or branch capture.</summary>
        public CaptureSource Source { get; }
        /// <summary>Main is hook zero; future branches have distinct positive IDs.</summary>
        public int HookId { get; }
        /// <summary>Depth in metres when capture was committed.</summary>
        public float Depth { get; }
        /// <summary>Paused-aware game time when capture was committed.</summary>
        public double GameTime { get; }
        /// <summary>Gold value copied from this run's species snapshot.</summary>
        public int Gold { get; }
        /// <summary>Shell value copied from this run's species snapshot.</summary>
        public int Shells { get; }
        /// <summary>Creates a value record without retaining mutable resources.</summary>
        public CaptureRecord(FishRules fish, int fishInstanceId, CaptureSource source, int hookId, float depth, double gameTime)
        {
            SpeciesId = fish.Id; FishInstanceId = fishInstanceId; Source = source;
            HookId = hookId; Depth = depth; GameTime = gameTime; Gold = fish.Gold; Shells = fish.Shells;
        }
    }

    /// <summary>Single species settlement row with a source breakdown.</summary>
    public sealed class FishSummary
    {
        /// <summary>Species key.</summary>
        public string SpeciesId { get; internal set; }
        /// <summary>Main-hook count.</summary>
        public int MainCount { get; internal set; }
        /// <summary>All branch-hook count.</summary>
        public int BranchCount { get; internal set; }
        /// <summary>Total captured count.</summary>
        public int TotalCount => MainCount + BranchCount;
        /// <summary>Summed gold.</summary>
        public int Gold { get; internal set; }
        /// <summary>Summed shells.</summary>
        public int Shells { get; internal set; }
        /// <summary>Whether a future result card should show its branch badge.</summary>
        public bool HasBranch => BranchCount > 0;
    }

    /// <summary>Pure settlement aggregation for real or synthetic source records.</summary>
    public static class Settlement
    {
        /// <summary>Combines all sources into exactly one row per species.</summary>
        public static IReadOnlyList<FishSummary> Summarize(IEnumerable<CaptureRecord> records)
        {
            var rows = new Dictionary<string, FishSummary>();
            foreach (var record in records)
            {
                if (!rows.TryGetValue(record.SpeciesId, out var row))
                    rows.Add(record.SpeciesId, row = new FishSummary { SpeciesId = record.SpeciesId });
                if (record.Source == CaptureSource.Main) row.MainCount++; else row.BranchCount++;
                row.Gold += record.Gold; row.Shells += record.Shells;
            }
            var result = new List<FishSummary>(rows.Values);
            result.Sort((a, b) =>
            {
                int price = (b.Gold / b.TotalCount).CompareTo(a.Gold / a.TotalCount);
                return price != 0 ? price : string.CompareOrdinal(a.SpeciesId, b.SpeciesId);
            });
            return new ReadOnlyCollection<FishSummary>(result);
        }
    }
}
