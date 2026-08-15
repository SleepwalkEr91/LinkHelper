using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using LinkHelper.Linking;
using LinkHelper.Settings;

namespace LinkHelper.Players;

public sealed class PlayerTracker(
    GameController gameController,
    LinkHelperSettings settings,
    LinkStateEvaluator linkStateEvaluator)
{
    private readonly List<TrackedPlayer> _players = [];
    private readonly List<DiscoveredPlayer> _candidates = [];

    public IReadOnlyList<TrackedPlayer> Players => _players;
    public IReadOnlyList<DiscoveredPlayer> Candidates => _candidates;
    public string LastError { get; private set; } = "";

    public void Refresh(bool collectCandidates)
    {
        _players.Clear();
        _candidates.Clear();

        var self = gameController?.Player;
        if (self is not { IsValid: true }) return;

        try
        {
            if (gameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Player, out var players))
            {
                foreach (var entity in players.ToList())
                {
                    if (entity is not { IsValid: true, IsAlive: true }) continue;
                    if (entity.Id == self.Id) continue;

                    // Always the real link status, independent of the "Highlight unlinked
                    // players" toggle: the auto-caster relies on this to know who still needs a
                    // cast, so it must not go blind just because the highlight is turned off.
                    var isLinked = linkStateEvaluator.IsLinked(entity);
                    _players.Add(new TrackedPlayer(entity, isLinked));

                    if (collectCandidates)
                        _candidates.Add(new DiscoveredPlayer(entity, isLinked, linkStateEvaluator.DescribeBuffs(entity)));
                }
            }

            LastError = "";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }
}

public sealed record DiscoveredPlayer(Entity Entity, bool IsLinked, string Buffs);
