using ExileCore.PoEMemory.MemoryObjects;

namespace LuminaryHelper.Minions;

public sealed record TrackedMinion(
    Entity Entity,
    MinionKind Kind,
    string SourceSkill,
    MinionMatchSource MatchSource,
    bool IsLinked);
