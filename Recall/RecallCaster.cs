using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using LuminaryHelper.Casting;
using LuminaryHelper.Minions;
using LuminaryHelper.Settings;

namespace LuminaryHelper.Recall;

public sealed class RecallCaster(
    CastGuard castGuard,
    SkillReadiness skillReadiness,
    LuminaryHelperSettings settings)
{
    private readonly Stopwatch _sinceMercenaryRecall = Stopwatch.StartNew();
    private readonly Stopwatch _sinceMinionRecall = Stopwatch.StartNew();

    public string MercenaryStatus { get; private set; } = "Disabled";
    public string MinionStatus { get; private set; } = "Disabled";
    public int MercenaryRecallCount { get; private set; }
    public int MinionRecallCount { get; private set; }

    public void Update(IReadOnlyList<TrackedMinion> minions)
    {
        var mercenaries = minions.Where(m => m.Kind == MinionKind.Mercenary).ToList();
        var others = minions.Where(m => m.Kind != MinionKind.Mercenary).ToList();

        var fired = TryRecall(settings.MercenaryRecall, mercenaries, _sinceMercenaryRecall,
            status => MercenaryStatus = status, () => MercenaryRecallCount++);

        if (fired) return;

        TryRecall(settings.MinionRecall, others, _sinceMinionRecall,
            status => MinionStatus = status, () => MinionRecallCount++);
    }

    public int EffectiveGapMs(RecallSkillSettings recall)
    {
        if (!recall.UseSkillCooldown) return recall.CooldownMs;

        if (!skillReadiness.TryFind(recall.SkillInternalName.Value, out var skill))
            return recall.CooldownMs;

        var cooldownMs = (int)(skill.Cooldown * 1000f);
        return cooldownMs > 0 ? cooldownMs + recall.CooldownMarginMs : recall.CooldownMs.Value;
    }

    private bool TryRecall(
        RecallSkillSettings recall,
        IReadOnlyList<TrackedMinion> watched,
        Stopwatch sinceLastUse,
        Action<string> setStatus,
        Action countIt)
    {
        if (!recall.Enable)
        {
            setStatus("Off");
            return false;
        }

        if (recall.SkillKey.Value?.Key is null or Keys.None)
        {
            setStatus("No key set");
            return false;
        }

        if (sinceLastUse.ElapsedMilliseconds < EffectiveGapMs(recall))
        {
            setStatus("Waiting for cooldown");
            return false;
        }

        if (!castGuard.CanSendInput(out var blockReason))
        {
            setStatus(blockReason);
            return false;
        }

        if (recall.RequireSkillReady && !skillReadiness.IsReady(recall.SkillInternalName.Value))
        {
            setStatus("Skill is on cooldown or not found");
            return false;
        }

        if (!ShouldRecall(recall, watched, out var triggerReason))
        {
            setStatus(triggerReason);
            return false;
        }

        InputHelper.SendInputPress(recall.SkillKey.Value);
        sinceLastUse.Restart();
        countIt();
        setStatus($"Recalled - {triggerReason}");
        return true;
    }

    private static bool ShouldRecall(
        RecallSkillSettings recall,
        IReadOnlyList<TrackedMinion> watched,
        out string reason)
    {
        if (!recall.UseHealthTrigger && !recall.UseDistanceTrigger)
        {
            reason = "No trigger picked";
            return false;
        }

        if (watched.Count == 0)
        {
            reason = "Nothing to recall";
            return false;
        }

        foreach (var minion in watched)
        {
            var entity = minion.Entity;
            if (entity is not { IsValid: true, IsAlive: true }) continue;

            if (recall.UseHealthTrigger &&
                TryGetHealthPercent(entity, out var healthPercent) &&
                healthPercent <= recall.HealthThresholdPercent)
            {
                reason = $"{Describe(entity)} at {healthPercent:0}% life";
                return true;
            }

            if (recall.UseDistanceTrigger && entity.DistancePlayer >= recall.DistanceThreshold)
            {
                reason = $"{Describe(entity)} is {entity.DistancePlayer:0} away";
                return true;
            }
        }

        reason = "No trigger met";
        return false;
    }

    private static bool TryGetHealthPercent(Entity entity, out float percent)
    {
        percent = 0;

        if (!entity.TryGetComponent<Life>(out var life) || life == null) return false;
        if (life.MaxHP <= 0) return false;

        percent = life.CurHP * 100f / life.MaxHP;
        return true;
    }

    private static string Describe(Entity entity) =>
        string.IsNullOrWhiteSpace(entity.RenderName) ? "A minion" : entity.RenderName;
}
