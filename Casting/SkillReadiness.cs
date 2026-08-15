using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;

namespace LinkHelper.Casting;

public sealed class SkillReadiness(GameController gameController)
{
    public bool TryFind(string patternList, out ActorSkill result)
    {
        result = null;

        if (!gameController.Player.TryGetComponent<Actor>(out var actor) || actor == null) return false;

        var patterns = PatternMatching.Split(patternList).ToList();
        if (patterns.Count == 0) return false;

        foreach (var skill in actor.ActorSkills ?? [])
        {
            var name = skill?.InternalName;
            if (string.IsNullOrEmpty(name)) continue;
            if (!patterns.Any(pattern => name.Contains(pattern, StringComparison.OrdinalIgnoreCase))) continue;

            result = skill;
            return true;
        }

        return false;
    }

    public bool IsReady(string patternList) => TryFind(patternList, out var skill) && IsReady(skill);

    public static bool IsReady(ActorSkill skill)
    {
        if (skill == null) return false;
        if (!skill.CanBeUsed) return false;
        if (skill.Cooldown > 0 && skill.RemainingUses <= 0) return false;

        return true;
    }

    public List<SkillRow> DescribeAll()
    {
        var rows = new List<SkillRow>();

        if (!gameController.Player.TryGetComponent<Actor>(out var actor) || actor == null) return rows;

        foreach (var skill in actor.ActorSkills ?? [])
        {
            if (skill == null || string.IsNullOrEmpty(skill.InternalName)) continue;

            rows.Add(new SkillRow(
                skill.InternalName,
                skill.Name ?? "",
                skill.Cooldown,
                skill.RemainingUses,
                skill.TotalUses,
                (int)skill.CastTime.TotalMilliseconds,
                IsReady(skill)));
        }

        return rows.OrderBy(row => row.InternalName).ToList();
    }
}

public sealed record SkillRow(
    string InternalName,
    string DisplayName,
    float MaxCooldown,
    int RemainingUses,
    int TotalUses,
    int CastTimeMs,
    bool Ready);
