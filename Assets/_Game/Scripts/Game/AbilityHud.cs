using System;
using Fishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Two independent thumb-area commands rendered entirely from Core slot state.</summary>
    internal sealed class AbilityHud
    {
        private readonly Button _sonar, _branches;
        private readonly Text _sonarLabel, _branchLabel, _sonarStatus, _branchStatus;
        private readonly string _sonarName, _branchName;
        internal AbilityHud(UiWidgets ui, Transform parent, SonarConfig sonar, BranchHookConfig branches, Action fire, Action deploy)
        {
            _sonarName = sonar.displayName; _branchName = branches.displayName;
            _sonar = Build(ui, parent, "Sonar button", sonar.icon, new Vector2(0, .2f), new Vector2(116, 112), fire, out _sonarLabel, out _sonarStatus);
            _branches = Build(ui, parent, "Branch button", branches.icon, new Vector2(1, .2f), new Vector2(-116, 112), deploy, out _branchLabel, out _branchStatus);
        }
        private static Button Build(UiWidgets ui, Transform parent, string name, Sprite icon, Vector2 anchor, Vector2 position,
            Action clicked, out Text label, out Text status)
        {
            var button = ui.Button(name, parent, "", Vector2.zero, new Vector2(188, 100), clicked);
            UiWidgets.Place((RectTransform)button.transform, anchor, position, new Vector2(188, 100));
            label = button.GetComponentInChildren<Text>(); label.fontSize = 22;
            UiWidgets.Place(label.rectTransform, Vector2.one * .5f, new Vector2(12, 18), new Vector2(150, 40));
            ui.Picture(name + " icon", button.transform, icon, new Vector2(-72, 18), new Vector2(26, 30));
            status = ui.Label(name + " status", button.transform, "", new Vector2(0, -24), new Vector2(180, 32), 17);
            return button;
        }
        internal void Refresh(RunModel run, AbilityController abilities)
        {
            RefreshSlot(_sonar, _sonarLabel, _sonarStatus, abilities.Sonar, run, _sonarName, "仅下潜可用", false);
            RefreshSlot(_branches, _branchLabel, _branchStatus, abilities.Branches, run, _branchName, "仅上收可用", abilities.Branches.Model.Deployed);
        }
        private static void RefreshSlot(Button button, Text label, Text status, AbilitySlot slot, RunModel run, string name, string stateHint, bool deployed)
        {
            button.interactable = slot.CanActivate(run);
            string title = name + " " + slot.RemainingUses + "/" + slot.Rules.Uses;
            if (label.text != title) label.text = title;
            string hint = run.IsPaused ? "暂停中" : deployed ? "已展开" : slot.RemainingUses == 0 ? "次数用尽" :
                slot.CooldownRemaining > .0001f ? $"冷却 {Mathf.Ceil(slot.CooldownRemaining * 10) / 10:0.0} 秒" : button.interactable ? "点击释放" : stateHint;
            if (status.text != hint) status.text = hint;
        }
    }
}
