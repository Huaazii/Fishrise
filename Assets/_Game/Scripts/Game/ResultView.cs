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
        private readonly Text _depth, _gold, _shells, _empty, _scrollHint;
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
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(6, 18, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
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
            _scrollHint = ui.Label("Scroll hint", _frame, "上下滑动，查看全部收获", Vector2.zero, new Vector2(520, 30), 18, color: UiWidgets.Muted);
            var totals = ui.Panel("Reward totals", _frame, new Color(.05f, .17f, .23f));
            UiWidgets.Place(totals, new Vector2(.5f, 0), new Vector2(0, 246), new Vector2(548, 76));
            ui.Picture("Total coin icon", totals, ui.Art.coin, new Vector2(-208, 0), new Vector2(44, 44));
            ui.Picture("Total shell icon", totals, ui.Art.shell, new Vector2(66, 0), new Vector2(44, 44));
            _gold = ui.Label("Gold total", totals, "0", new Vector2(-100, 0), new Vector2(160, 54), 30);
            _shells = ui.Label("Shell total", totals, "0", new Vector2(170, 0), new Vector2(150, 54), 30);
            ui.AnchoredLabel("Experience note", _frame, "体验关卡不获得奖励和图鉴", new Vector2(.5f, 0), new Vector2(0, 184), new Vector2(550, 36), 19, color: UiWidgets.Muted);
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
            var card = _ui.Panel("Catch card " + row.SpeciesId, _content, new Color(.075f, .19f, .255f));
            _cards.Add(card);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 104;
            var icon = _ui.Picture("Fish icon", card, fish != null ? fish.sprite : null, Vector2.zero, new Vector2(90, 72));
            UiWidgets.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(58, 0), new Vector2(90, 72));
            _ui.AnchoredLabel("Fish name", card, UiText.Species(row.SpeciesId), new Vector2(0, .5f), new Vector2(220, 24), new Vector2(210, 48), 26, TextAnchor.MiddleLeft);
            _ui.AnchoredLabel("Quantity", card, "×" + row.TotalCount, new Vector2(1, .5f), new Vector2(-58, 22), new Vector2(80, 48), 28, TextAnchor.MiddleRight);
            var coin = _ui.Picture("Unit coin", card, _ui.Art.coin, Vector2.zero, new Vector2(20, 20));
            UiWidgets.Place(coin.rectTransform, new Vector2(0, .5f), new Vector2(126, -8), new Vector2(20, 20));
            var shell = _ui.Picture("Unit shell", card, _ui.Art.shell, Vector2.zero, new Vector2(20, 20));
            UiWidgets.Place(shell.rectTransform, new Vector2(0, .5f), new Vector2(224, -8), new Vector2(20, 20));
            _ui.AnchoredLabel("Unit gold", card, (row.Gold / row.TotalCount).ToString(), new Vector2(0, .5f), new Vector2(170, -8), new Vector2(60, 28), 20, TextAnchor.MiddleLeft);
            _ui.AnchoredLabel("Unit shells", card, (row.Shells / row.TotalCount).ToString(), new Vector2(0, .5f), new Vector2(268, -8), new Vector2(60, 28), 20, TextAnchor.MiddleLeft);
            for (int i = 0; i < (fish != null ? fish.rarity : 1); i++)
            {
                var star = _ui.Picture("Star " + i, card, _ui.Art.star, Vector2.zero, new Vector2(15, 15));
                UiWidgets.Place(star.rectTransform, new Vector2(0, .5f), new Vector2(123 + i * 19, -34), new Vector2(15, 15));
            }
            _ui.AnchoredLabel("Capture sources", card, $"主钩 ×{row.MainCount} / 分支 ×{row.BranchCount}", new Vector2(1, .5f),
                new Vector2(-141, -32), new Vector2(260, 28), 17, TextAnchor.MiddleRight, UiWidgets.Muted);
            if (row.HasBranch)
            {
                var badge = _ui.Panel("Branch badge", card, new Color(.08f, .38f, .41f));
                UiWidgets.Place(badge, new Vector2(1, .5f), new Vector2(-153, 22), new Vector2(98, 30));
                _ui.Label("Branch label", badge, "含分支", Vector2.zero, new Vector2(94, 28), 16);
            }
        }
        internal void Layout(Vector2 available)
        {
            float width = Mathf.Min(656, available.x - 48), height = Mathf.Min(1280, available.y - 48);
            UiWidgets.Place(_frame, Vector2.one * .5f, Vector2.zero, new Vector2(width, height));
            float viewHeight = Mathf.Min(696, height - 520);
            UiWidgets.Place((RectTransform)Scroll.transform, new Vector2(.5f, 1), new Vector2(0, -214 - viewHeight * .5f), new Vector2(width - 80, viewHeight));
            UiWidgets.Place(_scrollHint.rectTransform, new Vector2(.5f, 1), new Vector2(0, -234 - viewHeight), new Vector2(width - 100, 30));
            if (Root.activeInHierarchy) Rebuild();
        }
        internal void Tick(float dt)
        {
            if (!Root.activeInHierarchy) return;
            _age += dt;
            for (int i = 0; i < _cards.Count; i++)
            {
                float t = Mathf.Clamp01((_age - i * .055f) / .26f);
                _cards[i].localScale = new Vector3(Mathf.Lerp(.12f, 1, Mathf.SmoothStep(0, 1, t)), 1, 1);
            }
        }
        private void Rebuild()
        {
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Scroll.StopMovement(); Scroll.verticalNormalizedPosition = 1;
            _scrollHint.gameObject.SetActive(_content.rect.height > _viewport.rect.height + 1);
        }
    }
}
