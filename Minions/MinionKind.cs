namespace LuminaryHelper.Minions;

public enum MinionKind
{
    Spectre,
    Mercenary,
    AnimateGuardian,
}

public enum MinionMatchSource
{
    DeployedObject,
    AlliedMonster,
}

public static class MinionKinds
{
    public static readonly MinionKind[] All =
    [
        MinionKind.Spectre,
        MinionKind.Mercenary,
        MinionKind.AnimateGuardian,
    ];
}
