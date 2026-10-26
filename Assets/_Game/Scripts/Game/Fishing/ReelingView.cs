using Fishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Non-interactive, scalable left-of-hook control bar; screen instructions belong to the shared bottom hint.</summary>
    internal sealed class ReelingView
    {
        internal readonly RectTransform Root;
        private readonly RectTransform _safe, _green, _fish, _stability;
        private readonly Image _greenImage, _stabilityImage, _marker;
        private readonly Text _status, _title;
        private readonly FishDefinition[] _species;
        private string _shownSpecies;
        private readonly Camera _camera;
        private bool _positioned;
        private const float TrackHeight = 224;
        internal ReelingView(UiWidgets ui, Transform parent, RectTransform safe, Camera camera, FishDefinition[] species)
        {
            _safe = safe; _camera = camera; _species = species;
            Root = ui.Panel("Reeling panel", parent, new Color(.025f, .09f, .14f, .92f));
            UiWidgets.Place(Root, Vector2.one * .5f, Vector2.zero, new Vector2(94, 310));
            _title = ui.Label("Reeling title", Root, "收线", new Vector2(0, 133), new Vector2(86, 32), 16);
            _title.resizeTextForBestFit = true; _title.resizeTextMinSize = 12; _title.resizeTextMaxSize = 16;
            var track = ui.Panel("Reeling track", Root, new Color(.13f, .35f, .43f));
            UiWidgets.Place(track, Vector2.one * .5f, new Vector2(-10, 3), new Vector2(46, TrackHeight + 8));
            var water = ui.Panel("Reeling water", track, new Color(.035f, .16f, .24f)); UiWidgets.Stretch(water, 4, 4, 4, 4);
            _green = ui.Panel("Reeling green zone", water, new Color(.35f, .88f, .32f, .85f));
            UiWidgets.Place(_green, Vector2.one * .5f, Vector2.zero, new Vector2(38, 94)); _greenImage = _green.GetComponent<Image>();
            _fish = ui.Picture("Reeling fish marker", water, species[0].sprite, Vector2.zero,
                new Vector2(38, TrackHeight * ReelingModel.MarkerHalfHeight * 2)).rectTransform;
            _marker = _fish.GetComponent<Image>();
            var stabilityTrack = ui.Panel("Reeling stability track", Root, new Color(.09f, .22f, .26f));
            UiWidgets.Place(stabilityTrack, Vector2.one * .5f, new Vector2(27, 3), new Vector2(12, TrackHeight));
            _stability = ui.Panel("Reeling stability", stabilityTrack, new Color(1, .8f, .23f));
            _stability.anchorMin = _stability.anchorMax = new Vector2(.5f, 0); _stability.pivot = new Vector2(.5f, 0);
            _status = ui.Label("Reeling status", Root, "稳固 60%", new Vector2(0, -132), new Vector2(90, 32), 17);
            _stabilityImage = _stability.GetComponent<Image>();
            Hide();
        }
        internal void Hide() { Root.gameObject.SetActive(false); _positioned = false; }
        internal void ResetRun() { Hide(); _shownSpecies = null; }
        internal void Refresh(RunModel run, Vector3 hook, Vector2 shake, float dt)
        {
            bool visible = run != null && run.Rules.Reeling.Enabled && run.State == RunState.Ascending &&
                !run.IsPaused && !run.IsFull && run.TotalCaptureCount > 0 && run.Reeling.Active;
            if (!visible) { Hide(); return; }
            Root.gameObject.SetActive(true);
            var reel = run.Reeling;
            string risk = run.EscapeCandidate?.SpeciesId;
            if (_shownSpecies != risk)
            {
                foreach (var definition in _species) if (definition.speciesId == risk)
                { _marker.sprite = definition.sprite; _title.text = "保住" + UiText.Species(risk, definition.displayName); break; }
                _shownSpecies = risk;
            }
            _green.sizeDelta = new Vector2(38, TrackHeight * reel.BarHeight);
            _green.anchoredPosition = new Vector2(0, (reel.BarPosition - .5f) * TrackHeight);
            _fish.anchoredPosition = new Vector2(0, (reel.FishPosition - .5f) * TrackHeight);
            _greenImage.color = reel.InZone ? new Color(.42f, .96f, .32f, .95f) : new Color(.25f, .63f, .35f, .8f);
            _stability.sizeDelta = new Vector2(10, TrackHeight * reel.Stability);
            _stabilityImage.color = reel.Stability < .3f ? UiWidgets.Full : new Color(1, .8f, .23f);
            _status.text = (reel.GraceRemaining > 0 ? "缓冲 " : "稳固 ") + Mathf.RoundToInt(reel.Stability * 100) + "%";
            // Cancel camera-only shake so the small precision-control target remains readable.
            Vector3 screen = _camera.WorldToScreenPoint(hook + (Vector3)shake);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_safe, screen, null, out var point);
            Vector2 target = point + new Vector2(-108, 132);
            Rect area = _safe.rect;
            target.x = Mathf.Clamp(target.x, area.xMin + 59, area.xMax - 59);
            target.y = Mathf.Clamp(target.y, area.yMin + area.height * .4f + 40, area.yMax - 375);
            Vector2 eased = _positioned ? Vector2.Lerp(Root.anchoredPosition, target, 1 - Mathf.Exp(-dt / .08f)) : target;
            // Clamp again after a resolution/safe-area change, including the interpolated position.
            eased.x = Mathf.Clamp(eased.x, area.xMin + 59, area.xMax - 59);
            eased.y = Mathf.Clamp(eased.y, area.yMin + 195, area.yMax - 375);
            Root.anchoredPosition = eased; _positioned = true;
        }
    }
}
