using System.Collections.Generic;
using DailyRoutines.Abstracts;
using DailyRoutines.Helpers;
using DailyRoutines.Managers;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OmenTools;
using OmenTools.Helpers;
using OmenTools.Infos;

namespace DailyRoutines.ModuleTemplate;

public class AutoSoulsowModuleTemplate : DailyModuleBase
{
    public override ModuleInfo Info => new()
    {
        Title = "自动播魂种 (模块模板)",
        Description = "Daily Routines 本地模块模板",
        Category = ModuleCategories.Action,
    };

    public override void Init()
    {
        TaskHelper ??= new TaskHelper { TimeLimitMS = 30_000 };

        DService.ClientState.TerritoryChanged += OnZoneChanged;
        DService.DutyState.DutyRecommenced    += OnDutyRecommenced;
        DService.Condition.ConditionChange    += OnConditionChanged;
    }

    public override void ConfigUI()
    {
        ImGui.Text("测试用文本");

        if (ImGui.Button("测试用按钮"))
        {
            NotifyHelper.Chat("测试点击了一下");
            HelpersOm.NotificationInfo("测试点了一下");
        }
        
        ImGui.Text("现在又改了一点东西");
    }

    // 重新挑战
    private void OnDutyRecommenced(object? sender, ushort e)
    {
        TaskHelper.Abort();
        TaskHelper.Enqueue(CheckCurrentJob);
    }

    // 进入副本
    private void OnZoneChanged(ushort zone)
    {
        if (LuminaCache.GetRow<TerritoryType>(zone) is not { ContentFinderCondition.Row: > 0 }) return;

        TaskHelper.Abort();
        TaskHelper.Enqueue(CheckCurrentJob);
    }
    
    // 战斗状态
    private void OnConditionChanged(ConditionFlag flag, bool value)
    {
        if (flag is not ConditionFlag.InCombat) return;
        
        TaskHelper.Abort();
        if (!value) TaskHelper.Enqueue(CheckCurrentJob);
    }

    private bool? CheckCurrentJob()
    {
        if (InfosOm.BetweenAreas || !HelpersOm.IsScreenReady() || InfosOm.OccupiedInEvent) return false;
        if (DService.Condition[ConditionFlag.InCombat] || 
            DService.ClientState.LocalPlayer is not { ClassJob.Id: 39 } || !IsValidPVEDuty())
        {
            TaskHelper.Abort();
            return true;
        }
        
        TaskHelper.Enqueue(UseRelatedActions, "UseRelatedActions", 5_000, true, 1);
        return true;
    }
    
    private unsafe bool? UseRelatedActions()
    {
        if (DService.ClientState.LocalPlayer is not { } localPlayer) return false;
        var statusManager = localPlayer.ToBCStruct()->StatusManager;

        // 播魂种
        if (statusManager.HasStatus(2594) || !HelpersOm.IsActionUnlocked(24387))
        {
            TaskHelper.Abort();
            return true;
        }

        TaskHelper.Enqueue(() => UseActionManager.UseAction(ActionType.Action, 24387), $"UseAction_{24387}",
                           5_000, true, 1);
        TaskHelper.DelayNext(2_000);
        TaskHelper.Enqueue(CheckCurrentJob, "SecondCheck", null, true, 1);
        return true;
    }

    private static unsafe bool IsValidPVEDuty()
    {
        HashSet<uint> InvalidContentTypes = [16, 17, 18, 19, 31, 32, 34, 35];

        var isPVP = GameMain.IsInPvPArea() || GameMain.IsInPvPInstance();
        var contentData = LuminaCache.GetRow<ContentFinderCondition>(GameMain.Instance()->CurrentContentFinderConditionId);
        
        return !isPVP && (contentData == null || !InvalidContentTypes.Contains(contentData.ContentType.Row));
    }

    public override void Uninit()
    {
        DService.ClientState.TerritoryChanged -= OnZoneChanged;
        DService.DutyState.DutyRecommenced    -= OnDutyRecommenced;
        DService.Condition.ConditionChange    -= OnConditionChanged;

        base.Uninit();
    }
}
