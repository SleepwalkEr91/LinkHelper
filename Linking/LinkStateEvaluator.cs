using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using LuminaryHelper.Minions;
using LuminaryHelper.Settings;

namespace LuminaryHelper.Linking;

public sealed class LinkStateEvaluator(GameController gameController, LuminaryHelperSettings settings)
{
    public bool IsLinked(Entity minion)
    {
        if (minion is not { IsValid: true }) return false;

        var patterns = MinionClassifier.SplitPatterns(settings.FlameLink.BuffPatterns.Value).ToList();
        if (patterns.Count == 0) return false;

        var buffs = minion.Buffs;
        if (buffs == null) return false;

        var playerId = gameController.Player?.Id ?? 0;
        var expiryThreshold = settings.FlameLinkTuning.TreatAsUnlinkedBelowSeconds.Value;

        foreach (var buff in buffs)
        {
            var name = buff?.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (!patterns.Any(pattern => name.Contains(pattern, StringComparison.OrdinalIgnoreCase))) continue;

            if (settings.FlameLinkTuning.OnlyMyLinks && playerId != 0 && buff.SourceEntityId != playerId) continue;
            if (expiryThreshold > 0 && buff.Timer > 0 && buff.Timer < expiryThreshold) continue;

            return true;
        }

        return false;
    }

    public string DescribeBuffs(Entity entity)
    {
        var buffs = entity?.Buffs;
        if (buffs == null || buffs.Count == 0) return "";

        var playerId = gameController.Player?.Id ?? 0;
        var parts = new List<string>();

        foreach (var buff in buffs)
        {
            var name = buff?.Name;
            if (string.IsNullOrEmpty(name)) continue;

            var mine = playerId != 0 && buff.SourceEntityId == playerId ? "*" : "";
            var remaining = buff.Timer is > 0 and < 1e6f ? $"({buff.Timer:0.#}s)" : "";
            parts.Add($"{mine}{name}{remaining}");
        }

        return string.Join(", ", parts);
    }
}
