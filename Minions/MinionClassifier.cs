using System;
using System.Collections.Generic;
using System.Linq;
using LuminaryHelper.Settings;

namespace LuminaryHelper.Minions;

public sealed class MinionClassifier(LuminaryHelperSettings settings)
{
    public MinionKind? Classify(string metadata, string skillInternalName)
    {
        foreach (var kind in MinionKinds.All)
        {
            var rules = settings.ForKind(kind);
            if (!rules.Enable) continue;

            if (MatchesAny(skillInternalName, rules.SkillPatterns.Value)) return kind;
            if (MatchesAny(metadata, rules.MetadataPatterns.Value)) return kind;
        }

        return null;
    }

    private static bool MatchesAny(string value, string patternList)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        return SplitPatterns(patternList)
            .Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<string> SplitPatterns(string patternList)
    {
        if (string.IsNullOrWhiteSpace(patternList)) return [];

        return patternList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
