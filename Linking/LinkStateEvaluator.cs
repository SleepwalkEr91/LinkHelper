using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using LinkHelper.Casting;
using LinkHelper.Players;
using LinkHelper.Settings;

namespace LinkHelper.Linking;

public sealed class LinkStateEvaluator(
    GameController gameController,
    LinkHelperSettings settings,
    LinkCastHistory castHistory,
    SkillReadiness skillReadiness)
{
    // Re-reading the skill's Stats dictionary means walking the whole ActorSkills list every
    // time - fine once, wasteful once per player once RescanIntervalMs is turned down low. The
    // duration barely ever changes (only via support gem swaps or a level-up), so a full second
    // of staleness is a non-issue and not worth exposing as its own setting.
    private static readonly TimeSpan DurationCacheFor = TimeSpan.FromMilliseconds(1000);
    private readonly Stopwatch _sinceDurationRead = Stopwatch.StartNew();
    private float _cachedDurationSeconds;

    public bool IsLinked(Entity player)
    {
        if (player is not { IsValid: true }) return false;
        if (!HasTargetBuff(player)) return false;

        return !IsAboutToExpire(player);
    }

    private bool HasTargetBuff(Entity player)
    {
        var patterns = PatternMatching.Split(settings.Link.TargetBuffPattern.Value).ToList();
        if (patterns.Count == 0) return false;

        var buffs = player.Buffs;
        if (buffs == null) return false;

        var myId = gameController.Player?.Id ?? 0;

        foreach (var buff in buffs)
        {
            var name = buff?.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (!patterns.Any(pattern => name.Contains(pattern, StringComparison.OrdinalIgnoreCase))) continue;

            if (settings.LinkTuning.OnlyMyLinks && myId != 0 && buff.SourceEntityId != myId) continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether, based on our own cast history rather than anything read from the target's buff
    /// (the game doesn't report that), the link on this player should be about to run out. Only
    /// kicks in once a duration is known (override setting, or read off the skill itself) and we
    /// ourselves have cast on this exact player before - otherwise there's nothing to go on, so
    /// it does not second-guess the plain buff-presence check above.
    /// </summary>
    private bool IsAboutToExpire(Entity player)
    {
        var durationSeconds = ResolveDurationSeconds();
        if (durationSeconds <= 0) return false;

        var key = PlayerIdentity.KeyFor(player);
        if (!castHistory.TryGetSecondsSinceLastCast(key, out var secondsSinceCast)) return false;

        var marginSeconds = settings.LinkTuning.RelinkMarginSeconds.Value;
        return secondsSinceCast >= durationSeconds - marginSeconds;
    }

    /// <summary>
    /// The link duration to re-cast by. The override setting wins whenever it is set above 0;
    /// otherwise this reads GameStat.BuffEffectDuration straight off the link skill's own Stats
    /// (falling back to SkillEffectDuration, which held the same value in testing), in
    /// milliseconds, confirmed via a live dump - both include support gem modifiers, so a
    /// duration-changing support is already accounted for. That dictionary was seen empty at
    /// least once during testing despite the same data showing up in ExileCore's own dev tree
    /// moments later, cause unconfirmed (maybe the game only computes it once a skill's tooltip
    /// has been generated this session) - so this stays defensive: a miss just returns 0, which
    /// the caller already treats as "duration unknown, fall back to plain buff-presence
    /// checking" exactly like the override being left at 0, never a wrong early re-cast. It
    /// self-heals the next time this is read once the data is actually there. Also used by
    /// LinkCaster to confirm a cast actually landed (comparing the source buff's timer against
    /// this value), not just for the early re-cast check above.
    /// </summary>
    public float ResolveDurationSeconds()
    {
        var overrideSeconds = settings.LinkTuning.LinkDurationOverrideSeconds.Value;
        if (overrideSeconds > 0) return overrideSeconds;

        if (_sinceDurationRead.Elapsed < DurationCacheFor) return _cachedDurationSeconds;
        _sinceDurationRead.Restart();

        _cachedDurationSeconds = ReadDurationFromSkillStats();
        return _cachedDurationSeconds;
    }

    private float ReadDurationFromSkillStats()
    {
        if (!skillReadiness.TryFind(settings.Link.SkillInternalName.Value, out var skill) || skill?.Stats == null)
            return 0f;

        if (skill.Stats.TryGetValue(GameStat.BuffEffectDuration, out var durationMs) ||
            skill.Stats.TryGetValue(GameStat.SkillEffectDuration, out durationMs))
            return durationMs / 1000f;

        return 0f;
    }

    /// <summary>
    /// Current remaining time on your own "source" buff, for confirming that a cast we just sent
    /// actually connected (see LinkCaster) - not for anything target-specific, since this buff
    /// isn't tied to one target once several links are active. False means either the buff is not
    /// currently up, its pattern is not configured, or the game is not reporting a usable timer
    /// for it right now.
    /// </summary>
    public bool TryGetSourceBuffTimerSeconds(out float seconds)
    {
        seconds = 0f;

        var patterns = PatternMatching.Split(settings.Link.SourceBuffPattern.Value).ToList();
        if (patterns.Count == 0) return false;

        var buffs = gameController.Player?.Buffs;
        if (buffs == null) return false;

        foreach (var buff in buffs)
        {
            var name = buff?.Name;
            if (string.IsNullOrEmpty(name)) continue;
            if (!patterns.Any(pattern => name.Contains(pattern, StringComparison.OrdinalIgnoreCase))) continue;
            if (buff.Timer is not (> 0 and < 1e6f)) continue;

            seconds = buff.Timer;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether you currently carry the "source" buff a link skill puts on the caster. Purely
    /// informational - shown in the status line and discovery window - and does NOT affect
    /// IsLinked, since a single cast of these skills can apparently keep several players linked
    /// at once and this buff can't be tied to one specific target. Leave "Link buff on you
    /// (source)" empty in the settings to skip this check (then this always returns true).
    /// </summary>
    public bool HasActiveSourceBuff()
    {
        var patterns = PatternMatching.Split(settings.Link.SourceBuffPattern.Value).ToList();
        if (patterns.Count == 0) return true;

        var buffs = gameController.Player?.Buffs;
        if (buffs == null) return false;

        foreach (var buff in buffs)
        {
            var name = buff?.Name;
            if (!string.IsNullOrEmpty(name) &&
                patterns.Any(pattern => name.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    public string DescribeBuffs(Entity entity)
    {
        var buffs = entity?.Buffs;
        if (buffs == null || buffs.Count == 0) return "";

        var myId = gameController.Player?.Id ?? 0;
        var parts = new List<string>();

        foreach (var buff in buffs)
        {
            var name = buff?.Name;
            if (string.IsNullOrEmpty(name)) continue;

            var mine = myId != 0 && buff.SourceEntityId == myId ? "*" : "";
            var remaining = buff.Timer is > 0 and < 1e6f ? $"({buff.Timer:0.#}s)" : "";
            parts.Add($"{mine}{name}{remaining}");
        }

        return string.Join(", ", parts);
    }
}
