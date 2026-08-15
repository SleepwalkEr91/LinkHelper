using System.Collections.Generic;
using System.Diagnostics;

namespace LinkHelper.Linking;

/// <summary>
/// The game doesn't expose a readable remaining-duration timer for the link buff on someone
/// else's character - only whether it's present, no countdown. Your own "source" buff does show
/// a duration, but it's a single buff that gets refreshed by every cast, so with more than one
/// link active at once it can't be tied to a specific target either. So instead of reading any
/// timer from the game, this remembers - per player, by name - how long ago you yourself last
/// successfully pressed the link key on them. Combined with a known buff duration (typed in from
/// the skill's tooltip, see "Known link duration" in the settings), that's enough to re-cast a
/// little before the buff should run out, without needing anything the game won't tell you.
/// </summary>
public sealed class LinkCastHistory
{
    private readonly Dictionary<string, Stopwatch> _sinceLastCast = new();

    public void RecordCast(string playerKey)
    {
        if (string.IsNullOrEmpty(playerKey)) return;

        if (_sinceLastCast.TryGetValue(playerKey, out var stopwatch))
            stopwatch.Restart();
        else
            _sinceLastCast[playerKey] = Stopwatch.StartNew();
    }

    public bool TryGetSecondsSinceLastCast(string playerKey, out float seconds)
    {
        seconds = 0f;

        if (string.IsNullOrEmpty(playerKey) || !_sinceLastCast.TryGetValue(playerKey, out var stopwatch))
            return false;

        seconds = (float)stopwatch.Elapsed.TotalSeconds;
        return true;
    }
}
