namespace Fishing.Game
{
    /// <summary>Chinese display names and three-page tutorial copy, separate from rules and asset IDs.</summary>
    internal static class UiText
    {
        internal static readonly string[] TutorialTitles = { "下潜时，避开鱼群", "两次保护，谨慎使用", "上收控条，保住高价值鱼" };
        internal static readonly string[] TutorialBodies =
        {
            "左右拖动控制鱼钩，不再划分操作区域。\n下潜时避开鱼群，上收时用鱼钩钩鱼头。\n获得冰蓝宝物可触发声呐。",
            "每局拥有 2 次保护，触碰鱼儿消耗 1 次。\n保护耗尽后再碰到鱼儿，会提前上收。\n进入水底区域后，保护不再生效。",
            "点按微调；原地按住，绿区上升；松开下落。\n拖动时优先移动，同一手指不同时收线。\n让鱼标留在绿区，稳固会逐渐恢复。\n失稳先逃高价值鱼，主钩满载后极速返航。\n淡金宝物展开分钩，但只能钓一星鱼。"
        };
        internal static string TutorialBody(int page, FishingControlMode mode)
        {
            if (mode != FishingControlMode.Tilt) return TutorialBodies[page];
            if (page == 0) return "左右轻倾设备控制鱼钩，回正停住。\n下潜避开鱼群，上收钩住鱼头。\n倒计时自动校准，暂停设置可重新校准。";
            if (page == 2) return "任意空白处按住，绿区上升；松开下落。\n左右轻倾选择路线，不必双指占屏。\n让鱼标留在绿区，稳固会逐渐恢复。\n失稳先逃高价值鱼，主钩满载后极速返航。\n淡金宝物展开分钩，但只能钓一星鱼。";
            return TutorialBodies[page];
        }
        internal static string Species(string id, string fallback = null)
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
                default: return fallback ?? id;
            }
        }
    }
}
