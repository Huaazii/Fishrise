using System;
using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Scrollable, source-aware fish cards rendered from the authoritative capture records.</summary>
    internal sealed class ResultView
    {
        internal readonly GameObject Root;
        internal readonly ScrollRect Scroll;
        internal IReadOnlyList<FishSummary> Rows { get; private set; } = Array.Empty<FishSummary>();
        private readonly RectTransform _frame, _content, _viewport;
        private readonly Text _depth, _gold, _shells, _empty;
        private readonly UiWidgets _ui;
        private readonly Dictionary<string, FishDefinition> _species = new Dictionary<string, FishDefinition>();
        private readonly List<RectTransform> _cards = new List<RectTransform>();
        private float _age;
        internal ResultView(UiWidgets ui, Transform parent, FishDefinition[] definitions, Action restart, Action exit)
        {
            _ui = ui; foreach (var fish in definitions) _species.Add(fish.speciesId, fish);
            Root = ui.Overlay("Result", parent, .72f); _frame = ui.Frame("Result card", Root.transform);
            ui.AnchoredLabel("Result title", _frame, "本次收获", new Vector2(.5f, 1), new Vector2(0, -76), new Vector2(520, 74), 46);
            _depth = ui.AnchoredLabel("Result depth", _frame, "", new Vector2(.5f, 1), new Vector2(0, -140), new Vector2(520, 44), 24, color: UiWidgets.Muted);
            var scrollRoot = UiWidgets.Rect("Catch scroll", _frame); Scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _viewport = UiWidgets.Rect("Catch viewport", scrollRoot); _viewport.gameObject.AddComponent<RectMask2D>();
            var input = _viewport.gameObject.AddComponent<Image>(); input.color = new Color(0, 0, 0, .015f); input.raycastTarget = true;
            _content = UiWidgets.Rect("Catch content", _viewport); _content.anchorMin = new Vector2(0, 1); _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(.5f, 1); _content.sizeDelta = Vector2.zero;
            var layout = _content.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 2;
            layout.cellSize = new Vector2(264, 200); layout.spacing = new Vector2(12, 12);
            layout.padding = new RectOffset(6, 6, 6, 6); layout.childAlignment = TextAnchor.UpperCenter;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Scroll.content = _content; Scroll.viewport = _viewport; Scroll.horizontal = false; Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped; Scroll.scrollSensitivity = 35;
            var bar = ui.Panel("Catch scrollbar", scrollRoot, new Color(.12f, .24f, .3f));
            bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(1, .5f);
            bar.offsetMin = new Vector2(-8, 0); bar.offsetMax = Vector2.zero;
            var handle = ui.Panel("Handle", bar, UiWidgets.Accent);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop; Scroll.verticalScrollbar = scrollbar;
            Scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            _empty = ui.Label("Empty catch", _viewport, "这一钩还没有收获\n换条路线，再试一次", Vector2.zero, new Vector2(500, 130), 28, color: UiWidgets.Muted);
            var totals = ui.Panel("Reward totals", _frame, new Color(.05f, .17f, .23f));
            UiWidgets.Place(totals, new Vector2(.5f, 0), new Vector2(0, 246), new Vector2(548, 76));
            ui.Picture("Total coin icon", totals, ui.Art.coin, new Vector2(-208, 0), new Vector2(44, 44));
            ui.Picture("Total shell icon", totals, ui.Art.shell, new Vector2(66, 0), new Vector2(44, 44));
            _gold = ui.Label("Gold total", totals, "0", new Vector2(-100, 0), new Vector2(160, 54), 30);
            _shells = ui.Label("Shell total", totals, "0", new Vector2(170, 0), new Vector2(150, 54), 30);
            ui.AnchoredLabel("Experience note", _frame, "本局收益仅作展示，不累计保存", new Vector2(.5f, 0), new Vector2(0, 184), new Vector2(550, 36), 19, color: UiWidgets.Muted);
            var back = ui.Button("Back to menu", _frame, "退出", new Vector2(-176, 0), new Vector2(190, 80), exit, false);
            var again = ui.Button("Dive again", _frame, "再来一局", new Vector2(104, 0), new Vector2(330, 80), restart);
            UiWidgets.Place((RectTransform)back.transform, new Vector2(.5f, 0), new Vector2(-176, 112), new Vector2(190, 80));
            UiWidgets.Place((RectTransform)again.transform, new Vector2(.5f, 0), new Vector2(104, 112), new Vector2(330, 80));
            Root.SetActive(false);
        }
        internal void Show(RunModel run)
        {
            _cards.Clear(); _age = 0;
            Root.SetActive(true);
            for (int i = _content.childCount - 1; i >= 0; i--)
            { var child = _content.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
            Rows = Settlement.Summarize(run.Captures);
            foreach (var row in Rows) Card(row);
            _depth.text = $"下潜深度：{run.ReachedDepth:0} 米"; _gold.text = run.Gold.ToString(); _shells.text = run.Shells.ToString();
            _empty.gameObject.SetActive(Rows.Count == 0); Rebuild();
        }
        private void Card(FishSummary row)
        {
            _species.TryGetValue(row.SpeciesId, out var fish);
            var card = UiWidgets.Rect("Catch card " + row.SpeciesId, _content);
            card.gameObject.AddComponent<ResultCardGraphic>().raycastTarget = false;
            _cards.Add(card);
            int rarity = fish != null ? fish.rarity : 1;
            for (int i = 0; i < rarity; i++)
            {
                var star = _ui.Picture("Star " + i, card, _ui.Art.star, Vector2.zero, new Vector2(14, 14));
                UiWidgets.Place(star.rectTransform, new Vector2(0, 1), new Vector2(22 + i * 16, -16), new Vector2(14, 14));
            }
            var quantityBadge = UiWidgets.Rect("Quantity badge", card);
            UiWidgets.Place(quantityBadge, new Vector2(1, 1), new Vector2(-42, -18), new Vector2(58, 28));
            var badgeGraphic = quantityBadge.gameObject.AddComponent<ResultCardGraphic>();
            badgeGraphic.QuantityBadge = true; badgeGraphic.raycastTarget = false;
            _ui.AnchoredLabel("Quantity", card, "×" + row.TotalCount, new Vector2(1, 1), new Vector2(-42, -18),
                new Vector2(54, 28), 18, TextAnchor.MiddleCenter, new Color(1, .97f, .88f));
            var icon = _ui.Picture("Fish icon", card, fish != null ? fish.sprite : null, Vector2.zero, new Vector2(176, 84));
            UiWidgets.Place(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(176, 84));
            var name = _ui.AnchoredLabel("Fish name", card, UiText.Species(row.SpeciesId), new Vector2(.5f, .5f), new Vector2(0, -28),
                new Vector2(230, 30), 20, TextAnchor.MiddleCenter);
            var coin = _ui.Picture("Unit coin", card, _ui.Art.coin, Vector2.zero, new Vector2(16, 16));
            UiWidgets.Place(coin.rectTransform, new Vector2(.5f, .5f), new Vector2(-52, -58), new Vector2(16, 16));
            var gold = _ui.AnchoredLabel("Unit gold", card, (row.Gold / row.TotalCount).ToString(), new Vector2(.5f, .5f), new Vector2(-30, -58),
                new Vector2(50, 30), 20, TextAnchor.MiddleLeft);
            gold.rectTransform.pivot = new Vector2(0, .5f);
            var shell = _ui.Picture("Unit shell", card, _ui.Art.shell, Vector2.zero, new Vector2(16, 16));
            UiWidgets.Place(shell.rectTransform, new Vector2(.5f, .5f), new Vector2(26, -58), new Vector2(16, 16));
            var shells = _ui.AnchoredLabel("Unit shells", card, (row.Shells / row.TotalCount).ToString(), new Vector2(.5f, .5f), new Vector2(48, -58),
                new Vector2(50, 30), 20, TextAnchor.MiddleLeft);
            shells.rectTransform.pivot = new Vector2(0, .5f);
            string sourcesText = row.BranchCount == 0 ? $"主钩 ×{row.MainCount}" :
                row.MainCount == 0 ? $"分支 ×{row.BranchCount}" : $"主钩 ×{row.MainCount} / 分支 ×{row.BranchCount}";
            var sources = _ui.AnchoredLabel("Capture sources", card, sourcesText, new Vector2(.5f, .5f),
                new Vector2(0, -84), new Vector2(220, 22), 13, TextAnchor.MiddleCenter, UiWidgets.Muted);
            if (row.HasBranch)
            {
                var badge = _ui.Panel("Branch badge", card, new Color(.08f, .38f, .41f));
                UiWidgets.Place(badge, new Vector2(0, 1), new Vector2(46 + rarity * 16, -16), new Vector2(48, 20));
                _ui.Label("Branch label", badge, "分支", Vector2.zero, new Vector2(44, 20), 12);
            }
        }
        internal void Layout(Vector2 available)
        {
            float width = Mathf.Min(656, available.x - 48), height = Mathf.Min(1280, available.y - 48);
            UiWidgets.Place(_frame, Vector2.one * .5f, Vector2.zero, new Vector2(width, height));
            float viewHeight = Mathf.Min(696, height - 520);
            UiWidgets.Place((RectTransform)Scroll.transform, new Vector2(.5f, 1), new Vector2(0, -214 - viewHeight * .5f), new Vector2(width - 80, viewHeight));
            if (Root.activeInHierarchy) Rebuild();
        }
        internal void Tick(float dt)
        {
            if (!Root.activeInHierarchy) return;
            _age += dt;
            for (int i = 0; i < _cards.Count; i++)
            {
                float t = Mathf.Clamp01((_age - i * .055f) / .26f);
                _cards[i].localScale = Vector3.one * Mathf.Lerp(.96f, 1, Mathf.SmoothStep(0, 1, t));
            }
        }
        private void Rebuild()
        {
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Scroll.StopMovement(); Scroll.verticalNormalizedPosition = 1;
        }
    }
}
