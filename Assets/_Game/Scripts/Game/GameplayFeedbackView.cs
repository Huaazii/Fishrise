using Fishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Reusable screen-space capture labels, shield bubbles and full-load speed lines.</summary>
    internal sealed class GameplayFeedbackView
    {
        private readonly RectTransform _root, _safe;
        private readonly Camera _camera;
        private readonly Text[] _labels = new Text[16];
        private readonly Vector2[] _origins = new Vector2[16];
        private readonly float[] _ages = new float[16];
        private readonly Image[] _bubbles = new Image[8], _lines = new Image[8];
        private Vector2 _burstOrigin;
        private float _burstAge = 1, _time;
        private int _next;
        internal bool SpeedLinesVisible { get; private set; }
        internal int VisibleLabels
        { get { int count = 0; foreach (var label in _labels) if (label.gameObject.activeSelf) count++; return count; } }
        internal GameplayFeedbackView(UiWidgets ui, RectTransform safe, Camera camera)
        {
            _safe = safe; _camera = camera; _root = UiWidgets.Rect("Gameplay feedback", safe);
            for (int i = 0; i < _labels.Length; i++)
                _labels[i] = ui.Label("Capture popup " + i, _root, "", Vector2.zero, new Vector2(220, 48), 25, color: new Color(1, .9f, .48f));
            for (int i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i] = ui.Picture("Shield burst bubble " + i, _root, ui.Art.bubble, Vector2.zero, Vector2.one * 30);
                _lines[i] = ui.Picture("Full speed line " + i, _root, null, Vector2.zero, new Vector2(3, 90));
                _lines[i].preserveAspect = false; _lines[i].color = new Color(.56f, .91f, 1, .5f);
            }
            Reset();
        }
        private Vector2 ScreenPosition(Vector3 world)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle(_safe, _camera.WorldToScreenPoint(world), null, out var p); return p; }
        internal void Capture(CaptureRecord record, Vector3 world)
        {
            int i = _next++ % _labels.Length; _ages[i] = 0; _origins[i] = ScreenPosition(world);
            _labels[i].text = (record.Source == CaptureSource.Branch ? "分支 " : "") + "+" + record.Gold;
            _labels[i].gameObject.SetActive(true); _labels[i].rectTransform.anchoredPosition = _origins[i];
        }
        internal void Burst(Vector3 world) { _burstOrigin = ScreenPosition(world); _burstAge = 0; }
        internal void Tick(RunModel run, float dt)
        {
            bool gameplay = run.State == RunState.Diving || run.State == RunState.BottomHold || run.State == RunState.Ascending;
            _root.gameObject.SetActive(gameplay); if (!gameplay) return;
            _time += dt; _burstAge += dt;
            for (int i = 0; i < _labels.Length; i++)
            {
                _ages[i] += dt; bool active = _ages[i] < .55f; _labels[i].gameObject.SetActive(active);
                if (!active) continue;
                var p = _origins[i] + new Vector2((i % 3 - 1) * 28, 70 * _ages[i] / .55f);
                p.x = Mathf.Clamp(p.x, _safe.rect.xMin + 116, _safe.rect.xMax - 116);
                p.y = Mathf.Clamp(p.y, _safe.rect.yMin + 180, _safe.rect.yMax - 240);
                _labels[i].rectTransform.anchoredPosition = p;
                _labels[i].color = new Color(1, .9f, .48f, Mathf.Clamp01((.55f - _ages[i]) * 6));
            }
            SpeedLinesVisible = run.State == RunState.Ascending && run.IsFull;
            for (int i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i].gameObject.SetActive(_burstAge < .45f);
                if (_burstAge < .45f)
                {
                    float a = i * Mathf.PI / 4, t = _burstAge / .45f;
                    _bubbles[i].rectTransform.anchoredPosition = _burstOrigin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (20 + 110 * t);
                    _bubbles[i].color = new Color(.64f, .95f, 1, 1 - t);
                }
                _lines[i].gameObject.SetActive(SpeedLinesVisible);
                if (!SpeedLinesVisible) continue;
                var rect = _lines[i].rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(i % 2 == 0 ? .045f : .955f, 0);
                rect.anchoredPosition = new Vector2(0, Mathf.Repeat(i * 171 - _time * 700, _safe.rect.height - 300) + 100);
                rect.sizeDelta = new Vector2(2 + i % 2, 64 + i % 3 * 28);
            }
        }
        internal void Reset()
        {
            _time = 0; _next = 0; _burstAge = 1; SpeedLinesVisible = false;
            for (int i = 0; i < _labels.Length; i++) { _ages[i] = 1; _labels[i].gameObject.SetActive(false); }
            foreach (var bubble in _bubbles) bubble.gameObject.SetActive(false);
            foreach (var line in _lines) line.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }
    }
}
