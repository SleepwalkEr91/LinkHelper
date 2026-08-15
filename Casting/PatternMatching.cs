using System;
using System.Collections.Generic;

namespace LinkHelper.Casting;

/// <summary>
/// Small helper for the comma separated, partial-match pattern lists used across the settings
/// (buff names, skill internal names). Split out of the old minion classifier so it can be
/// shared without dragging in anything minion-specific.
/// </summary>
public static class PatternMatching
{
    public static IEnumerable<string> Split(string patternList)
    {
        if (string.IsNullOrWhiteSpace(patternList)) return [];

        return patternList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
