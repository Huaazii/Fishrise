using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Read-only, scalable fish reference built directly from the scene's species assets.</summary>
    internal sealed class FishCatalogView
    {
        internal readonly GameObject Root;
        internal readonly ScrollRect Scroll;
        internal IReadOnlyList<FishDefinition> Entries => _entries;
        private readonly FishDefinition[] _definitions;
        private readonly List<FishDefinition> _entries = new List<FishDefinition>();
        private readonly List<Row> _rows = new List<Row>();
        private readonly UiWidgets _ui;
        private readonly RectTransform _frame, _content, _viewport;
        private readonly Text _hint, _empty;

        private sealed class Row
        {
            internal RectTransform Root;
            internal Image Fish;
            internal Text Order, Name, Gold, Shells, Depth, Kind;
            internal RectTransform Stars;
            internal readonly Image[] Rarity = new Image[5];
        }

        internal FishCatalogView(UiWidgets ui, Transform parent, FishDefinition[] definitions, Action close)
        {
            _ui = ui; _definitions = definitions;
            Root = ui.Overlay("Fish catalog", parent, .88f); _frame = ui.Frame("Catalog card", Root.transform);
            ui.AnchoredLabel("Catalog title", _frame, "鱼类图鉴", new Vector2(.5f, 1), new Vector2(0, -76), new Vector2(520, 74), 44);
            var scrollRoot = UiWidgets.Rect("Catalog scroll", _frame); Scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _empty=ui.Label("Catalog empty",scrollRoot,"暂无可查看的鱼类",Vector2.zero,new Vector2(480,50),23,color:UiWidgets.Muted);
            _viewport = UiWidgets.Rect("Catalog viewport", scrollRoot); _viewport.gameObject.AddComponent<RectMask2D>();
            var input = _viewport.gameObject.AddComponent<Image>(); input.color = new Color(0, 0, 0, .015f);
            _content = UiWidgets.Rect("Catalog content", _viewport);
            _content.anchorMin = new Vector2(0, 1); _content.anchorMax = Vector2.one; _content.pivot = new Vector2(.5f, 1);
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12; layout.padding = new RectOffset(6, 6, 8, 8);
            layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Scroll.content = _content; Scroll.viewport = _viewport; Scroll.horizontal = false; Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped; Scroll.scrollSensitivity = 40;
            _hint = ui.AnchoredLabel("Catalog hint", _frame, "上下滑动，查看全部鱼类", new Vector2(.5f, 0), new Vector2(0, 156), new Vector2(520, 32), 20, color: UiWidgets.Muted);
            var back = ui.Button("Close catalog", _frame, "返回主界面", Vector2.zero, new Vector2(520, 80), close, false);
            UiWidgets.Place((RectTransform)back.transform, new Vector2(.5f, 0), new Vector2(0, 94), new Vector2(520, 80));
            Root.SetActive(false);
        }

        internal void Open()
        {
            _entries.Clear();
            foreach (var fish in _definitions) if (fish != null && fish.isCatchable) _entries.Add(fish);
            _entries.Sort((a, b) => {
                int gold = b.gold.CompareTo(a.gold); if (gold != 0) return gold;
                int shells = b.shells.CompareTo(a.shells); return shells != 0 ? shells : string.CompareOrdinal(a.speciesId, b.speciesId);
            });
            while (_rows.Count < _entries.Count) _rows.Add(CreateRow());
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i]; row.Root.gameObject.SetActive(i < _entries.Count); if (i >= _entries.Count) continue;
                var fish = _entries[i]; row.Root.name = "Catalog row " + fish.speciesId;
                row.Order.text = (i + 1).ToString("00"); row.Name.text = UiText.Species(fish.speciesId, fish.displayName);
                row.Fish.sprite = fish.sprite; row.Gold.text = fish.gold.ToString(); row.Shells.text = fish.shells.ToString();
                row.Depth.text = $"{fish.minDepth:0.#}–{fish.maxDepth:0.#} 米";
                row.Kind.text = fish.isSmall ? "小鱼" : "中大型鱼";
                int rarity=Mathf.Clamp(fish.rarity,1,row.Rarity.Length);
                row.Stars.sizeDelta=new Vector2(rarity*22-2,24);
                for(int star=0;star<row.Rarity.Length;star++) row.Rarity[star].gameObject.SetActive(star<rarity);
            }
            _empty.gameObject.SetActive(_entries.Count==0);
            Root.SetActive(true); Root.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Scroll.StopMovement(); Scroll.verticalNormalizedPosition = 1;
            _hint.gameObject.SetActive(_content.rect.height > _viewport.rect.height + 1);
        }

        private Row CreateRow()
        {
            var root = UiWidgets.Rect("Catalog row",_content);
            var surface=root.gameObject.AddComponent<ResultCardGraphic>(); surface.SpotlightX=-.30f; surface.raycastTarget=false;
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 176;
            var row = new Row { Root = root };
            row.Order = _ui.AnchoredLabel("Catalog rank", root, "", new Vector2(0, .5f), new Vector2(26, 0), new Vector2(30, 40), 19, color: UiWidgets.Muted);
            row.Fish = _ui.Picture("Catalog fish", root, null, Vector2.zero, new Vector2(110, 116));
            UiWidgets.Place(row.Fish.rectTransform, new Vector2(0, .5f), new Vector2(104, 0), new Vector2(110, 116));
            var info = UiWidgets.Rect("Catalog info", root); UiWidgets.Stretch(info, 166, 10, 14, 10);
            row.Name = Line("Catalog name", info, .82f, 0, .62f, 28); row.Name.fontStyle=FontStyle.Bold;
            row.Stars=UiWidgets.Rect("Catalog rarity",info);
            UiWidgets.Place(row.Stars,new Vector2(1,.82f),new Vector2(-4,0),new Vector2(108,24)); row.Stars.pivot=new Vector2(1,.5f);
            for(int i=0;i<row.Rarity.Length;i++)
            {
                row.Rarity[i]=_ui.Picture("Catalog star "+i,row.Stars,_ui.Art.star,Vector2.zero,new Vector2(20,20));
                UiWidgets.Place(row.Rarity[i].rectTransform,new Vector2(0,.5f),new Vector2(10+i*22,0),new Vector2(20,20));
            }
            row.Gold = Line("Catalog gold", info, .52f, 0, .5f, 24);
            row.Shells = Line("Catalog shells", info, .52f, .5f, 1, 24);
            row.Gold.fontStyle=row.Shells.fontStyle=FontStyle.Bold;
            row.Gold.color=new Color(1,.87f,.50f);
            row.Gold.rectTransform.offsetMin=row.Shells.rectTransform.offsetMin=new Vector2(38,-20);
            Icon("Catalog gold icon",info,_ui.Art.coin,0,.52f,28);
            Icon("Catalog shells icon",info,_ui.Art.shell,.5f,.52f,28);
            row.Depth = Line("Catalog depth", info, .22f, 0, .62f, 19);
            row.Depth.rectTransform.offsetMin=new Vector2(32,-20); row.Depth.color=UiWidgets.Muted;
            Icon("Catalog depth icon",info,_ui.Art.depth,0,.22f,22);
            row.Kind=Line("Catalog size",info,.22f,.62f,1,18); row.Kind.alignment=TextAnchor.MiddleRight; row.Kind.color=UiWidgets.Muted;
            return row;
        }

        private void Icon(string name,Transform parent,Sprite sprite,float x,float y,float size)
        {
            var icon=_ui.Picture(name,parent,sprite,Vector2.zero,Vector2.one*size);
            UiWidgets.Place(icon.rectTransform,new Vector2(x,y),new Vector2(14,0),Vector2.one*size);
        }

        private Text Line(string name, RectTransform parent, float y, float left, float right, int fontSize)
        {
            var label = _ui.Label(name, parent, "", Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleLeft);
            var rect = label.rectTransform; rect.anchorMin = new Vector2(left, y); rect.anchorMax = new Vector2(right, y);
            rect.offsetMin = new Vector2(0, -20); rect.offsetMax = new Vector2(-4, 20); return label;
        }

        internal void Close() { Root.SetActive(false); Scroll.StopMovement(); }
        internal void Layout(Vector2 available)
        {
            float width = Mathf.Min(656, available.x - 48), height = Mathf.Min(1480, available.y - 48);
            UiWidgets.Place(_frame, Vector2.one * .5f, Vector2.zero, new Vector2(width, height));
            float viewHeight = height - 312;
            UiWidgets.Place((RectTransform)Scroll.transform, new Vector2(.5f, 1), new Vector2(0, -132 - viewHeight * .5f), new Vector2(width - 80, viewHeight));
        }
    }
}
