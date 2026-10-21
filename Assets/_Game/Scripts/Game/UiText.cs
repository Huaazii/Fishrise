namespace Fishing.Game
{
    /// <summary>Chinese display names and three-page tutorial copy, separate from rules and asset IDs.</summary>
    internal static class UiText
    {
        internal static readonly string[] TutorialTitles = { "下潜时，避开鱼头", "两次保护，谨慎使用", "上收捕获，满载返航" };
        internal static readonly string[] TutorialBodies =
        {
            "按住屏幕左右拖动，控制鱼钩。\n下潜时避开鱼头，尽量到达更深处。\n声呐可驱散附近小鱼，每局 2 次。",
            "每局拥有 2 次保护，触碰鱼头消耗 1 次。\n保护耗尽后再碰到鱼头，会提前上收。\n进入水底区域后，保护不再生效。",
            "上收时，用钩头接触鱼头捕获。\n主钩最多挂 10 条，满载后加速返回。\n分散鱼钩额外钩小鱼，不占主容量。\n深处的稀有鱼，收获更高。"
        };
        internal static string Species(string id)
        {
            switch (id)
            {
                case "puffer": return "河豚";
                case "blue": return "蓝纹鱼";
                case "red": return "红鱼";
                case "small": return "小红鱼";
                case "yellow": return "黄蝴蝶鱼";
                case "eel": return "鳗鱼";
                case "silver": return "小银鱼";
                default: return id;
            }
        }
    }
}
