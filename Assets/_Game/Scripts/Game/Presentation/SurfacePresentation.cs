using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Collider-free fishrise, rod recoil and delayed rewards driven only by the gameplay clock.</summary>
    public sealed class SurfacePresentation : System.IDisposable
    {
        private sealed class Jump
        {
            internal RectTransform Root;
            internal UnityEngine.UI.Text Amount;
            internal UnityEngine.UI.Image Coin;
            internal CanvasGroup Group;
            internal DeformedSprite Fish;
            internal Vector3 CarryOffset, FanOffset, Scale, Landing, RewardPosition, ReleasePosition;
            internal float Delay, RewardDelay, Angle, Bend, Head, ReleaseAngle;
            internal bool Launched, Landed, Rewarded;
            internal int HookId;
        }
        private readonly List<Jump> _pool = new List<Jump>();
        private readonly Dictionary<string, FishDefinition> _species = new Dictionary<string, FishDefinition>();
        private readonly UiWidgets _ui;
        private readonly RectTransform _root, _safe;
        private readonly Camera _camera;
        private readonly LineRenderer[] _drops = new LineRenderer[12];
        private Transform _world, _airHook;
        private HookMotor _hook;
        private FishSpawner _spawner;
        private FishingSceneView _scene;
        private SurfaceLighting _lighting;
        private Material _fishMaterial;
        private Vector3 _origin, _landing;
        private float _duration, _scale, _flight, _rewardDuration;
        private int _count, _mainCount;
        private IReadOnlyList<BranchHookView> _branches;
        private bool[] _surfaceBranches;
        private Vector3[] _branchOrigins;
        /// <summary>Gameplay-clock elapsed time since the surface phase began.</summary>
        public float Elapsed { get; private set; }
        /// <summary>Fish that have detached from the airborne hook.</summary>
        public int LaunchedCount { get; private set; }
        /// <summary>Branch fish released after the main sequence has started.</summary>
        public int LaunchedBranchCount { get; private set; }
        /// <summary>Fish that have completed their decorative landing.</summary>
        public int LandedCount { get; private set; }
        /// <summary>Number of immutable captures scheduled for this presentation.</summary>
        public int PlannedCount => _count;
        /// <summary>Reusable fish view count, independent of the current run's rewards.</summary>
        public int AllocatedCount => _pool.Count;
        /// <summary>Airborne visual hook; it has no physics components.</summary>
        public Transform AirHook => _airHook;
        /// <summary>Fish or reward views still visible, including fish carried before release.</summary>
        public int ActiveCount
        { get { int count = 0; foreach (var jump in _pool) if (jump.Fish.Renderer.gameObject.activeSelf || jump.Root.gameObject.activeSelf) count++; return count; } }
        /// <summary>World fish currently carried or flying.</summary>
        public int WorldFishCount
        { get { int count = 0; foreach (var jump in _pool) if (jump.Fish.Renderer.gameObject.activeSelf) count++; return count; } }
        /// <summary>Reward labels currently visible, separate from the fish silhouettes.</summary>
        public int VisibleRewardCount
        { get { int count = 0; foreach (var jump in _pool) if (jump.Root.gameObject.activeSelf) count++; return count; } }

        internal SurfacePresentation(UiWidgets ui, RectTransform parent, FishDefinition[] species, Camera camera)
        {
            _ui = ui; _safe = parent; _camera = camera; foreach (var fish in species) _species.Add(fish.speciesId, fish);
            _root = UiWidgets.Rect("Surfacing show", parent);
            var heading = ui.Panel("Surface heading", _root, new Color(.02f, .09f, .14f, .8f));
            UiWidgets.Place(heading, new Vector2(.5f, 1), new Vector2(0, -68), new Vector2(400, 76));
            ui.Label("Surface title", heading, "这一钩的收获", Vector2.zero, new Vector2(380, 60), 34);
            _root.gameObject.SetActive(false);
        }
        /// <summary>Connects existing scene art once; no prefab, collider, source texture or material is cloned.</summary>
        public void BindWorld(Transform parent, HookMotor hook, FishSpawner spawner, FishingSceneView scene, IReadOnlyList<BranchHookView> branches)
        {
            _hook = hook; _spawner = spawner; _scene = scene; _fishMaterial = scene.DeformationMaterial; _lighting = scene.GetComponent<SurfaceLighting>();
            _world = new GameObject("Fishrise world presentation").transform; _world.SetParent(parent, false);
            _airHook = hook.CreateSurfaceVisual(_world);
            _branches = branches; _surfaceBranches = new bool[branches.Count]; _branchOrigins = new Vector3[branches.Count];
            for (int i = 0; i < _drops.Length; i++)
            {
                var drop = new GameObject("Surface water drop " + i).AddComponent<LineRenderer>(); drop.transform.SetParent(_world, false);
                drop.sharedMaterial = hook.LineMaterial; drop.positionCount = 2; drop.useWorldSpace = true;
                drop.numCapVertices = 4; drop.sortingLayerName = "Surface Lit"; drop.sortingOrder = 45;
                drop.startWidth = .025f; drop.endWidth = .01f; drop.enabled = false; _drops[i] = drop;
            }
            for (int i = 0; i < 16; i++) AddJump();
            _world.gameObject.SetActive(false);
        }
        private void AddJump()
        {
            var root = UiWidgets.Rect("Surface fish " + _pool.Count, _root);
            UiWidgets.Place(root, Vector2.one * .5f, Vector2.zero, new Vector2(112, 34));
            var jump = new Jump { Root = root, Group = root.gameObject.AddComponent<CanvasGroup>() };
            jump.Group.blocksRaycasts = false; jump.Group.interactable = false;
            jump.Coin = _ui.Picture("Surface coin", root, _ui.Art.coin, Vector2.zero, new Vector2(24, 24));
            jump.Amount = _ui.Label("Surface gold", root, "", Vector2.zero, new Vector2(110, 32), 22, color: new Color(1, .89f, .43f));
            var outline = jump.Amount.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.015f, .055f, .075f, .95f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            jump.Fish = new DeformedSprite("Airborne fish " + _pool.Count, _world, _fishMaterial, 40 + _pool.Count % 3);
            root.gameObject.SetActive(false); jump.Fish.Renderer.gameObject.SetActive(false); _pool.Add(jump);
        }
        /// <summary>Copies held fish poses before hiding them, then schedules main and delayed branch releases.</summary>
        public void Begin(RunModel run)
        {
            for(int i=0;i<_branches.Count;i++)
            { _surfaceBranches[i]=_branches[i].gameObject.activeSelf; _branchOrigins[i]=_branches[i].transform.position; }
            Reset(); _count = run.Captures.Count; _mainCount = run.MainCaptureCount; _duration = run.Rules.SurfacingSeconds;
            _scale = _duration / 2.3f; _flight = .90f * _scale;
            _rewardDuration = Mathf.Min(.14f, .72f / Mathf.Max(1, _count - 1) * 2.9f) * _scale;
            _origin = _hook.transform.position; _landing = _scene.SurfaceCatchPoint;
            while (_pool.Count < _count) AddJump();
            float spacing = Mathf.Max(0, _duration - 2.10f * _scale) /
                Mathf.Max(1, Mathf.Max(run.MainCaptureCount, run.BranchCaptureCount) - 1);
            int index = 0;
            foreach (var source in new[] { CaptureSource.Main, CaptureSource.Branch })
            {
                int order = 0;
                foreach (var capture in run.Captures)
                {
                    if (capture.Source != source) continue;
                    var jump = _pool[index]; int ordinal = order++;
                    jump.Delay = .62f * _scale + ordinal * spacing + (source == CaptureSource.Branch ? .3f * _scale : 0);
                    jump.RewardDelay = (1.4f + index * .72f / Mathf.Max(1, _count - 1)) * _scale;
                    var definition = _species[capture.SpeciesId]; jump.Fish.SetSprite(definition.sprite);
                    jump.Bend = definition.sprite.bounds.size.x * (definition.speciesId == "eel" ? .11f : definition.speciesId == "puffer" ? .018f : .065f);
                    jump.Head = Mathf.Clamp01((definition.mouthPosition.x - definition.sprite.bounds.min.x) / definition.sprite.bounds.size.x);
                    _spawner.TryGetOwned(capture.FishInstanceId, out var held);
                    jump.HookId = capture.HookId;
                    var carrierOrigin = capture.HookId == 0 ? _origin : _branchOrigins[capture.HookId-1];
                    jump.CarryOffset = held != null ? held.Body.transform.position - carrierOrigin : Vector3.down * .2f;
                    jump.Scale = held != null ? held.Body.transform.lossyScale : Vector3.one * (definition.size * .65f);
                    jump.Angle = held != null ? held.Body.transform.eulerAngles.z : 80;
                    float side = ordinal % 2 == 0 ? -1 : 1;
                    jump.FanOffset = new Vector3(side * (.30f + ordinal % 3 * .24f + (source == CaptureSource.Branch ? .65f : 0)), -.25f - ordinal / 3 * .23f, 0);
                    jump.Landing = _landing + Vector3.right * ((index % 3 - 1) * .08f);
                    jump.Amount.text = "+" + capture.Gold;
                    float amountWidth = jump.Amount.preferredWidth + 4;
                    jump.Amount.rectTransform.sizeDelta = new Vector2(amountWidth, 32);
                    jump.Amount.rectTransform.anchoredPosition = new Vector2(15, 0);
                    jump.Coin.rectTransform.anchoredPosition = new Vector2(-(amountWidth + 6) * .5f, 0);
                    jump.Amount.color = new Color(1, .89f, .43f);
                    if (held != null) held.gameObject.SetActive(false);
                    index++;
                }
            }
            _root.gameObject.SetActive(true); _world.gameObject.SetActive(true); Tick(0);
        }
        private Vector3 HookPosition(float elapsed)
        {
            float t = elapsed / _scale;
            // First guide the decorative hook to the opening while it is still behind the ice.
            float lift = 1 - Mathf.Pow(1 - Mathf.Clamp01((t - .10f) / .38f), 3);
            float after = Mathf.Max(0, t - .48f);
            float rebound = Mathf.Sin(after * 17) * Mathf.Exp(-after * 4) * .14f;
            var attachment = _hook.SurfaceLineOffset(HookAngle(elapsed));
            // The tethered hook stays below the bent rod tip; only released fish fly above it.
            float ceiling = _scene.RodTip.position.y - attachment.y;
            float raisedY = ceiling - .68f;
            if (_count == 0) raisedY = Mathf.Min(raisedY, _origin.y + 2.1f);
            float y = Mathf.Lerp(_origin.y, raisedY, lift) - .18f * Mathf.SmoothStep(0, 1, after / .4f) + rebound;
            y = Mathf.Min(y, ceiling - .45f);
            // The whole hook, rather than its pivot alone, must fit inside the 1.5-unit opening.
            float x = 0;
            x += Mathf.Sin(after * 13) * Mathf.Exp(-after * 3.5f) * .16f;
            return new Vector3(x, y, 0);
        }
        private float HookAngle(float elapsed)
        {
            float t = elapsed / _scale;
            return -12 * Mathf.Sin(Mathf.Clamp01(t / .48f) * Mathf.PI) +
                (t < .48f ? 0 : 13 * Mathf.Sin((t - .48f) * 16) * Mathf.Exp(-(t - .48f) * 3.7f));
        }
        private Vector3 CarryPosition(Jump jump, float elapsed)
        {
            float t = elapsed / _scale;
            Vector3 carrier = jump.HookId == 0 ? HookPosition(elapsed) : BranchPosition(jump.HookId-1,elapsed);
            float spread = Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.8f,2.9f,carrier.y));
            var offset = Vector3.Lerp(jump.CarryOffset, jump.FanOffset, spread);
            offset.y -= .16f * Mathf.Sin(Mathf.Clamp01(t / .6f) * Mathf.PI);
            return carrier + Quaternion.Euler(0, 0, HookAngle(elapsed)) * offset;
        }
        private Vector3 BranchPosition(int index,float elapsed)
        {
            var position=HookPosition(Mathf.Max(0,elapsed-.055f*_scale)) - Vector3.up*.22f;
            float side=_branches.Count==1 ? 1 : Mathf.Lerp(-1,1,(float)index/(_branches.Count-1));
            float spread=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.8f,2.9f,position.y));
            position.x+=side*Mathf.Lerp(.12f,.65f,spread); return position;
        }
        private float CarryAngle(Jump jump, float elapsed, int index) => jump.Angle + HookAngle(elapsed) -
            9 * Mathf.Sin(elapsed / _scale * 8 + index) * Mathf.Clamp01(elapsed / _scale);
        /// <summary>Advances flight and rewards without changing capture records, gameplay transforms or payouts.</summary>
        public void Tick(float dt)
        {
            if (!_root.gameObject.activeSelf) return;
            Elapsed += dt; _scene.PresentSurface(Elapsed, _duration);
            _hook.PresentSurface(HookPosition(Elapsed), HookAngle(Elapsed));
            for(int i=0;i<_branches.Count;i++)
                if(_surfaceBranches[i]) _branches[i].PresentSurface(_airHook.position,BranchPosition(i,Elapsed),HookAngle(Elapsed)*.45f);
            TickWater();
            for (int i = 0; i < _count; i++)
            {
                var jump = _pool[i]; float age = Elapsed - jump.Delay;
                float t = Mathf.Clamp01(age / _flight);
                if (age >= 0 && !jump.Launched)
                {
                    // The rod keeps moving after release; a fish must retain its own launch pose.
                    jump.ReleasePosition = CarryPosition(jump, jump.Delay); jump.ReleaseAngle = CarryAngle(jump, jump.Delay, i);
                    jump.Launched = true; LaunchedCount++; if (i >= _mainCount) LaunchedBranchCount++;
                    _ui.Sound(GameFeedback.Cue.FishWhoosh);
                }
                if (age >= _flight && !jump.Landed) { jump.Landed = true; LandedCount++; _ui.Sound(GameFeedback.Cue.FishLand); }
                bool flying = age < _flight; jump.Fish.Renderer.gameObject.SetActive(flying);
                if (flying)
                {
                    var position = age < 0 ? CarryPosition(jump, Elapsed) : Vector3.Lerp(jump.ReleasePosition, jump.Landing, t) +
                        new Vector3((i % 2 == 0 ? -1 : 1) * .45f, 5.8f + i % 3 * .22f, 0) * (4 * t * (1 - t));
                    float angle = age < 0 ? CarryAngle(jump, Elapsed, i) :
                        jump.ReleaseAngle + (i % 2 == 0 ? -1 : 1) * (65 * t + 12 * Mathf.Sin(t * 12));
                    jump.Fish.Transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
                    float shrink = age < 0 ? 1 : (1 + .08f * Mathf.Sin(t * Mathf.PI)) * Mathf.Lerp(1, .45f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, t)));
                    jump.Fish.Transform.localScale = jump.Scale * shrink;
                    var tint = Color.Lerp(new Color(.67f, .84f, .94f), Color.white, Mathf.Clamp01((position.y + .2f) / 1.6f));
                    tint.a = age < 0 ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.90f, 1, t));
                    jump.Fish.SetPose(0, jump.Bend * Mathf.Clamp01(Elapsed / (.2f * _scale)), Elapsed / _scale * 14 + i * 1.7f, jump.Head, tint);
                    // Use the deformed silhouette including rotation, rather than the centre crossing the snow line.
                    var bounds=jump.Fish.Renderer.bounds;
                    if(bounds.min.y<1.02f && bounds.max.y>-1.8f)
                        jump.Fish.Transform.position+=Vector3.right*Mathf.Clamp(0,-.70f-bounds.min.x,.70f-bounds.max.x);
                    jump.Fish.Renderer.sortingLayerName = jump.Fish.Renderer.bounds.min.y < 1.02f ? "Default" : "Surface Lit";
                    _lighting?.ApplyLighting(jump.Fish.Renderer);
                }
                // Rewards arrive late, in separate lanes below the fish arcs.
                float rewardAge = Elapsed - jump.RewardDelay;
                if (rewardAge >= 0 && !jump.Rewarded) { jump.Rewarded = true; _ui.Sound(GameFeedback.Cue.Reward); }
                jump.Root.gameObject.SetActive(rewardAge >= 0 && rewardAge < _rewardDuration);
                jump.Group.alpha = Mathf.Clamp01((_rewardDuration - rewardAge) / (.05f * _scale));
                jump.RewardPosition = new Vector3(_landing.x - .65f - i % 3 * 1.5f, _landing.y + .6f + Mathf.Max(0, rewardAge) * .65f / _scale, 0);
            }
            RefreshScreenPositions();
        }
        private void TickWater()
        {
            float age = Elapsed / _scale - .08f;
            for (int i = 0; i < _drops.Length; i++)
            {
                var drop = _drops[i]; float a = age - i % 3 * .025f;
                bool active = a >= 0 && a < .42f; drop.enabled = active; if (!active) continue;
                float vx = (i % 2 == 0 ? -1 : 1) * (.7f + i % 4 * .45f), vy = 3.8f + i % 3 * .7f;
                var p = new Vector3(0, _origin.y, 0) + new Vector3(vx * a, .35f + vy * a - 10 * a * a, 0);
                var velocity = new Vector3(vx, vy - 20 * a, 0).normalized;
                drop.SetPosition(0, p); drop.SetPosition(1, p - velocity * (.08f + i % 3 * .035f));
                var color = new Color(.64f, .91f, 1, (1 - a / .42f) * .75f); drop.startColor = drop.endColor = color;
            }
        }
        /// <summary>Reprojects labels after camera follow without advancing the presentation clock.</summary>
        public void RefreshScreenPositions()
        {
            if (!_root.gameObject.activeSelf) return;
            for (int i = 0; i < _count; i++)
            {
                var jump = _pool[i]; if (!jump.Root.gameObject.activeSelf) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_safe, _camera.WorldToScreenPoint(jump.RewardPosition), null, out var p);
                p.x = Mathf.Clamp(p.x, _safe.rect.xMin + 60, _safe.rect.xMax - 60);
                p.y = Mathf.Clamp(p.y, _safe.rect.yMin + 180, _safe.rect.yMax - 126); jump.Root.anchoredPosition = p;
            }
        }
        /// <summary>Clears carried fish, flights, water and rewards immediately, retaining last-run counters.</summary>
        public void Hide()
        {
            _root.gameObject.SetActive(false); _hook?.EndSurface();
            // HUD refreshes also hide this panel during ascent; only an actual surface show owns the branch cleanup.
            if(_world!=null && _world.gameObject.activeSelf && _branches!=null)
                foreach(var branch in _branches) branch.ResetView();
            foreach (var jump in _pool) { jump.Root.gameObject.SetActive(false); jump.Fish.Renderer.gameObject.SetActive(false); }
            if (_world != null) _world.gameObject.SetActive(false);
            foreach (var drop in _drops) if (drop != null) drop.enabled = false;
        }
        /// <summary>Resets all decorative state on restart, including delayed branch releases.</summary>
        public void Reset()
        {
            Hide(); _scene?.ResetSurface(); Elapsed = 0; LaunchedCount = LaunchedBranchCount = LandedCount = 0; _count = _mainCount = 0;
            foreach (var jump in _pool) jump.Launched = jump.Landed = jump.Rewarded = false;
        }
        /// <summary>Releases scene-owned mesh buffers without touching already-destroying Unity hierarchy objects.</summary>
        public void Dispose() { foreach (var jump in _pool) jump.Fish.Dispose(); }
    }
}
