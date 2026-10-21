using System;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Three-page art-backed tutorial; closing reports whether the menu requested a new run.</summary>
    internal sealed class TutorialView
    {
        internal readonly GameObject Root;
        internal int Page { get; private set; }
        private readonly RectTransform _frame, _illustrations;
        private readonly Text _title, _body, _page;
        private readonly Button _previous, _next;
        private readonly Image[] _dots = new Image[3];
        private readonly GameObject[] _pages = new GameObject[3];
        private readonly Action<bool> _closed;
        private bool _startAfterClose;
        internal TutorialView(UiWidgets ui, Transform parent, Action<bool> closed)
        {
            _closed = closed; Root = ui.Overlay("Tutorial", parent, .72f); _frame = ui.Frame("Tutorial card", Root.transform);
            ui.AnchoredLabel("Tutorial heading", _frame, "钓鱼教学", new Vector2(.5f, .91f), Vector2.zero, new Vector2(430, 64), 40);
            var close = ui.Button("Tutorial close", _frame, "×", Vector2.zero, new Vector2(58, 58), Close, false);
            UiWidgets.Place((RectTransform)close.transform, new Vector2(.92f, .94f), Vector2.zero, new Vector2(58, 58));
            _title = ui.AnchoredLabel("Tutorial title", _frame, "", new Vector2(.5f, .80f), Vector2.zero, new Vector2(530, 96), 32);
            _body = ui.AnchoredLabel("Tutorial body", _frame, "", new Vector2(.5f, .33f), Vector2.zero, new Vector2(530, 230), 25);
            _illustrations = UiWidgets.Rect("Tutorial illustrations", _frame);
            UiWidgets.Place(_illustrations, new Vector2(.5f, .59f), Vector2.zero, new Vector2(530, 260));
            var art = ui.Art;
            for (int i = 0; i < 3; i++) _pages[i] = UiWidgets.Rect("Illustration " + i, _illustrations).gameObject;
            ui.Picture("Coral", _pages[0].transform, art.coral, new Vector2(-180, -78), new Vector2(155, 125));
            ui.Picture("Rock", _pages[0].transform, art.rocks, new Vector2(180, -95), new Vector2(140, 85));
            ui.Picture("Bluefish", _pages[0].transform, art.blueFish, new Vector2(90, 35), new Vector2(230, 120));
            ui.Picture("Purple fish", _pages[0].transform, art.purpleFish, new Vector2(-90, -40), new Vector2(175, 95));
            ui.Picture("Main hook", _pages[0].transform, art.hook, new Vector2(-70, 65), new Vector2(65, 130));
            ui.Picture("Protection one", _pages[1].transform, art.bubble, new Vector2(-112, 30), new Vector2(170, 170));
            ui.Picture("Protection two", _pages[1].transform, art.bubble, new Vector2(112, 30), new Vector2(170, 170));
            ui.Picture("Hook one", _pages[1].transform, art.hook, new Vector2(-112, 22), new Vector2(55, 115));
            ui.Picture("Hook two", _pages[1].transform, art.hook, new Vector2(112, 22), new Vector2(55, 115));
            ui.Picture("Silver school", _pages[1].transform, art.silverFish, new Vector2(0, -98), new Vector2(115, 45));
            ui.Picture("Orange conch", _pages[1].transform, art.orangeConch, new Vector2(-205, -92), new Vector2(85, 65));
            ui.Picture("Grey conch", _pages[1].transform, art.greyConch, new Vector2(205, -92), new Vector2(85, 65));
            ui.Picture("Catch hook", _pages[2].transform, art.hook, new Vector2(0, 30), new Vector2(65, 135));
            ui.Picture("Spotted catch", _pages[2].transform, art.spottedFish, new Vector2(65, -22), new Vector2(145, 80));
            ui.Picture("Silver catch", _pages[2].transform, art.silverFish, new Vector2(-65, -36), new Vector2(120, 55));
            ui.Picture("Catch barrel", _pages[2].transform, art.barrel, new Vector2(190, -56), new Vector2(125, 155));
            ui.Picture("Coin reward", _pages[2].transform, art.coin, new Vector2(-185, 42), new Vector2(74, 74));
            ui.Picture("Shell reward", _pages[2].transform, art.shell, new Vector2(-185, -45), new Vector2(70, 70));
            for (int i = 0; i < 3; i++)
            {
                var dot = ui.Panel("Page dot " + i, _frame, UiWidgets.Muted);
                UiWidgets.Place(dot, new Vector2(.5f, .17f), new Vector2((i - 1) * 28, 0), new Vector2(12, 12));
                _dots[i] = dot.GetComponent<Image>();
            }
            _page = ui.AnchoredLabel("Tutorial page", _frame, "", new Vector2(.5f, .17f), new Vector2(95, 0), new Vector2(70, 30), 18, color: UiWidgets.Muted);
            _previous = ui.Button("Tutorial previous", _frame, "上一页", new Vector2(-145, 0), new Vector2(190, 84), Previous, false);
            _next = ui.Button("Tutorial next", _frame, "下一页", new Vector2(110, 0), new Vector2(260, 84), Next);
            UiWidgets.Place((RectTransform)_previous.transform, new Vector2(.5f, .09f), new Vector2(-145, 0), new Vector2(190, 84));
            UiWidgets.Place((RectTransform)_next.transform, new Vector2(.5f, .09f), new Vector2(110, 0), new Vector2(260, 84));
            Root.SetActive(false);
        }
        internal void Open(bool startAfterClose) { _startAfterClose = startAfterClose; Page = 0; Root.SetActive(true); Refresh(); }
        internal void Close() { Root.SetActive(false); _closed(_startAfterClose); }
        private void Next() { if (Page < 2) { Page++; Refresh(); } else Close(); }
        private void Previous() { if (Page > 0) { Page--; Refresh(); } }
        private void Refresh()
        {
            _title.text = UiText.TutorialTitles[Page]; _body.text = UiText.TutorialBodies[Page]; _page.text = (Page + 1) + " / 3";
            for (int i = 0; i < 3; i++) { _pages[i].SetActive(i == Page); _dots[i].color = i == Page ? UiWidgets.Accent : UiWidgets.Muted; }
            _previous.interactable = Page > 0;
            UiWidgets.ButtonText(_next, Page == 2 ? (_startAfterClose ? "开始下潜" : "知道了") : "下一页");
        }
        internal void Layout(Vector2 available)
        {
            float width = Mathf.Min(656, available.x - 48), height = Mathf.Min(1120, available.y - 48);
            UiWidgets.Place(_frame, Vector2.one * .5f, Vector2.zero, new Vector2(width, height));
            _title.rectTransform.sizeDelta = new Vector2(width - 110, 96); _body.rectTransform.sizeDelta = new Vector2(width - 110, 230);
            _illustrations.localScale = Vector3.one * Mathf.Min(1, (width - 100) / 530);
        }
    }
}
