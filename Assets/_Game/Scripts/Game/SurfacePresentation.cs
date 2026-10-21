using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Bounded, skippable fish-and-coin presentation reading only immutable capture records.</summary>
    public sealed class SurfacePresentation
    {
        private sealed class Jump
        {
            internal RectTransform Root;
            internal Image Fish;
            internal Text Amount;
            internal CanvasGroup Group;
            internal float Delay;
            internal bool Launched;
        }
        private const float Flight = .46f;
        private readonly List<Jump> _pool = new List<Jump>();
        private readonly Dictionary<string, FishDefinition> _species = new Dictionary<string, FishDefinition>();
        private readonly UiWidgets _ui;
        private readonly RectTransform _root, _safe;
        private readonly Camera _camera;
        private int _count;
        /// <summary>Gameplay-clock elapsed time since the surface phase began.</summary>
        public float Elapsed { get; private set; }
        /// <summary>Number of individual fish launches already displayed.</summary>
        public int LaunchedCount { get; private set; }
        /// <summary>Number of captures in the presentation snapshot.</summary>
        public int PlannedCount => _count;
        /// <summary>Live jump views, for cleanup and acceptance checks.</summary>
        public int ActiveCount
        { get { int count = 0; foreach (var jump in _pool) if (jump.Root.gameObject.activeSelf) count++; return count; } }
        internal SurfacePresentation(UiWidgets ui, RectTransform parent, FishDefinition[] species, Camera camera, System.Action skip)
        {
            _ui = ui; _safe = parent; _camera = camera; foreach (var fish in species) _species.Add(fish.speciesId, fish);
            _root = UiWidgets.Rect("Surfacing show", parent);
            var heading = ui.Panel("Surface heading", _root, new Color(.02f, .09f, .14f, .8f));
            UiWidgets.Place(heading, new Vector2(.5f, 1), new Vector2(0, -68), new Vector2(400, 76));
            ui.Label("Surface title", heading, "这一钩的收获", Vector2.zero, new Vector2(380, 60), 34);
            var button = ui.Button("Skip surfacing", _root, "点击跳过", Vector2.zero, new Vector2(256, 66), skip, false);
            UiWidgets.Place((RectTransform)button.transform, new Vector2(.5f, 0), new Vector2(0, 214), new Vector2(256, 66));
            for (int i = 0; i < 16; i++) AddJump();
            Hide();
        }
        private void AddJump()
        {
            var root = UiWidgets.Rect("Surface fish " + _pool.Count, _root);
            UiWidgets.Place(root, Vector2.one * .5f, Vector2.zero, new Vector2(180, 150));
            var jump = new Jump { Root = root, Group = root.gameObject.AddComponent<CanvasGroup>() };
            jump.Group.blocksRaycasts = false; jump.Group.interactable = false;
            jump.Fish = _ui.Picture("Jump fish", root, null, Vector2.zero, new Vector2(78, 58));
            var coin = _ui.Panel("Surface coin chip", root, new Color(.025f, .09f, .14f, .9f));
            UiWidgets.Place(coin, Vector2.one * .5f, new Vector2(0, 49), new Vector2(140, 40));
            jump.Amount = _ui.Label("Surface gold", coin, "", Vector2.zero, new Vector2(134, 38), 24, color: new Color(1, .89f, .43f));
            root.gameObject.SetActive(false); _pool.Add(jump);
        }
        /// <summary>Schedules main fish in order; the corresponding branch sequence starts 0.3 seconds later.</summary>
        public void Begin(RunModel run)
        {
            Reset(); _count = run.Captures.Count;
            while (_pool.Count < _count) AddJump();
            float spacing = Mathf.Max(0, run.Rules.SurfacingSeconds - .32f - Flight) /
                Mathf.Max(1, Mathf.Max(run.MainCaptureCount, run.BranchCaptureCount) - 1);
            int index = 0;
            foreach (var source in new[] { CaptureSource.Main, CaptureSource.Branch })
            {
                int order = 0;
                foreach (var capture in run.Captures)
                {
                    if (capture.Source != source) continue;
                    var jump = _pool[index++]; jump.Delay = order++ * spacing + (source == CaptureSource.Branch ? .3f : 0);
                    _species.TryGetValue(capture.SpeciesId, out var fish); jump.Fish.sprite = fish != null ? fish.sprite : null;
                    jump.Amount.text = "+" + capture.Gold + (source == CaptureSource.Branch ? " 分支" : "");
                }
            }
            _root.gameObject.SetActive(true); Tick(0);
        }
        /// <summary>Advances only decorative views; this method never commits rewards.</summary>
        public void Tick(float dt)
        {
            if (!_root.gameObject.activeSelf) return;
            Elapsed += dt;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_safe, _camera.WorldToScreenPoint(Vector3.zero), null, out var origin);
            for (int i = 0; i < _count; i++)
            {
                var jump = _pool[i]; float age = Elapsed - jump.Delay;
                bool visible = age >= 0 && age < Flight; jump.Root.gameObject.SetActive(visible);
                if (age >= 0 && !jump.Launched) { jump.Launched = true; LaunchedCount++; }
                if (!visible) continue;
                float t = age / Flight, side = i % 2 == 0 ? -1 : 1;
                float x = side * (195 + i % 3 * 66) * t;
                var p = origin + new Vector2(x, Mathf.Sin(t * Mathf.PI) * (165 + i % 3 * 34) - t * 14);
                p.x = Mathf.Clamp(p.x, _safe.rect.xMin + 100, _safe.rect.xMax - 100);
                p.y = Mathf.Min(p.y, _safe.rect.yMax - 126);
                jump.Root.anchoredPosition = p;
                jump.Fish.rectTransform.localRotation = Quaternion.Euler(0, 0, side * Mathf.Lerp(-28, 32, t));
                jump.Fish.rectTransform.localScale = Vector3.one * (1 + .15f * Mathf.Sin(t * Mathf.PI));
                jump.Group.alpha = Mathf.Clamp01((1 - t) * 4);
            }
        }
        /// <summary>Hides all pending and active views immediately, retaining last-run instrumentation.</summary>
        public void Hide() { _root.gameObject.SetActive(false); foreach (var jump in _pool) jump.Root.gameObject.SetActive(false); }
        /// <summary>Clears all visual state on restart, including scheduled branch fish.</summary>
        public void Reset()
        { Hide(); Elapsed = 0; LaunchedCount = 0; _count = 0; foreach (var jump in _pool) jump.Launched = false; }
    }
}
