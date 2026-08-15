using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using LuminaryHelper.Linking;
using LuminaryHelper.Settings;

namespace LuminaryHelper.Minions;

public sealed class MinionTracker(
    GameController gameController,
    LuminaryHelperSettings settings,
    MinionClassifier classifier,
    LinkStateEvaluator linkStateEvaluator)
{
    private readonly List<TrackedMinion> _minions = [];
    private readonly List<DiscoveredCandidate> _candidates = [];

    public IReadOnlyList<TrackedMinion> Minions => _minions;
    public IReadOnlyList<DiscoveredCandidate> Candidates => _candidates;
    public string LastError { get; private set; } = "";

    public void Refresh(bool collectCandidates)
    {
        _minions.Clear();
        _candidates.Clear();

        var player = gameController?.Player;
        if (player is not { IsValid: true }) return;

        var seen = new HashSet<uint>();

        try
        {
            CollectDeployedObjects(player, seen, collectCandidates);

            if (settings.Advanced.ScanAlliedMonsters)
                CollectAlliedMonsters(seen, collectCandidates);

            LastError = "";
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private void CollectDeployedObjects(Entity player, HashSet<uint> seen, bool collectCandidates)
    {
        if (!player.TryGetComponent<Actor>(out var actor) || actor == null) return;

        var skillNames = BuildSkillNameLookup(actor);

        foreach (var deployed in actor.DeployedObjects.ToList())
        {
            var entity = deployed?.Entity;
            if (entity is not { IsValid: true, IsAlive: true }) continue;
            if (!seen.Add(entity.Id)) continue;

            var skillName = skillNames.GetValueOrDefault(deployed.SkillKey, "");
            Record(entity, skillName, MinionMatchSource.DeployedObject, collectCandidates);
        }
    }

    private static Dictionary<ushort, string> BuildSkillNameLookup(Actor actor)
    {
        var lookup = new Dictionary<ushort, string>();

        foreach (var skill in actor.ActorSkills ?? [])
        {
            if (skill == null) continue;

            var name = skill.InternalName;
            if (string.IsNullOrEmpty(name)) continue;

            lookup.TryAdd(skill.Id, name);
            lookup.TryAdd(skill.Id2, name);
        }

        return lookup;
    }

    private void CollectAlliedMonsters(HashSet<uint> seen, bool collectCandidates)
    {
        if (!gameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var monsters))
            return;

        foreach (var entity in monsters.ToList())
        {
            if (entity is not { IsValid: true, IsAlive: true, IsHostile: false }) continue;
            if (!seen.Add(entity.Id)) continue;

            Record(entity, "", MinionMatchSource.AlliedMonster, collectCandidates);
        }
    }

    private void Record(Entity entity, string skillName, MinionMatchSource source, bool collectCandidates)
    {
        var kind = classifier.Classify(entity.Metadata, skillName);

        if (kind is { } matched)
        {
            var isLinked = settings.FlameLink.TrackLinkState && linkStateEvaluator.IsLinked(entity);
            _minions.Add(new TrackedMinion(entity, matched, skillName, source, isLinked));
        }

        if (collectCandidates)
            _candidates.Add(new DiscoveredCandidate(
                entity, skillName, source, kind, linkStateEvaluator.DescribeBuffs(entity)));
    }
}

public sealed record DiscoveredCandidate(
    Entity Entity,
    string SourceSkill,
    MinionMatchSource MatchSource,
    MinionKind? MatchedKind,
    string Buffs);
