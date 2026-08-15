using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using LuminaryHelper.Minions;
using Newtonsoft.Json;

namespace LuminaryHelper.Settings;

public class LuminaryHelperSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(false);

    [Menu("Spectres", 100)]
    public MinionKindSettings Spectres { get; set; } = MinionKindSettings.ForSpectres();

    [Menu("Mercenaries", 200)]
    public MinionKindSettings Mercenaries { get; set; } = MinionKindSettings.ForMercenaries();

    [Menu("Animate Guardian", 300)]
    public MinionKindSettings AnimateGuardian { get; set; } = MinionKindSettings.ForAnimateGuardian();

    [Menu("Flame Link", 400)]
    public FlameLinkSettings FlameLink { get; set; } = new();

    [Menu("Flame Link tuning", 410)]
    public FlameLinkTuning FlameLinkTuning { get; set; } = new();

    [Menu("Recall: Mercenary", 500)]
    public RecallSkillSettings MercenaryRecall { get; set; } = RecallSkillSettings.ForMercenary();

    [Menu("Recall: everything else", 510)]
    public RecallSkillSettings MinionRecall { get; set; } = RecallSkillSettings.ForConvocation();

    [Menu("Display", 600)]
    public DisplaySettings Display { get; set; } = new();

    [Menu("Safety", 700)]
    public SafetySettings Safety { get; set; } = new();

    [Menu("Advanced", 800)]
    public AdvancedSettings Advanced { get; set; } = new();

    [Menu("Discovery", 900)]
    public DiscoverySettings Discovery { get; set; } = new();

    public MinionKindSettings ForKind(MinionKind kind) => kind switch
    {
        MinionKind.Spectre => Spectres,
        MinionKind.Mercenary => Mercenaries,
        MinionKind.AnimateGuardian => AnimateGuardian,
        _ => Spectres,
    };
}

[Submenu(CollapsedByDefault = false)]
public class FlameLinkSettings
{
    [Menu("Highlight unlinked minions", "Draws a minion in the colour below while your link is missing from it")]
    public ToggleNode TrackLinkState { get; set; } = new(true);

    [Menu("Unlinked colour")]
    public ColorNode UnlinkedColor { get; set; } = new(SharpDX.Color.Red);

    [Menu("Cast Flame Link for me", "Everything below stays idle until this is on and a key is set")]
    public ToggleNode AutoCast { get; set; } = new(false);

    [Menu("Flame Link key", "The key Flame Link sits on in game")]
    public HotkeyNodeV2 SkillKey { get; set; } = new(Keys.None);

    [Menu("Only while I hold a key", "Off means it re-links on its own")]
    public ToggleNode RequireHotkeyHeld { get; set; } = new(true);

    [Menu("Hold key", "Never sent to the game, only read")]
    public HotkeyNodeV2 CastHotkey { get; set; } = new(Keys.None);

    [Menu("Only link minions I picked", "Uses the Link targets list at the top. Off links all of them")]
    public ToggleNode LinkOnlySelected { get; set; } = new(false);

    [Menu("Link buff name", "Name of the buff a link puts on a minion")]
    public TextNode BuffPatterns { get; set; } = new("flame_link");

    [IgnoreMenu]
    public Dictionary<string, bool> SelectedTargets { get; set; } = new();
}

[Submenu(CollapsedByDefault = true)]
public class FlameLinkTuning
{
    [Menu("Match the skill's cast time", "Paces casts to Flame Link's real cast time instead of the fixed gap below")]
    public ToggleNode UseSkillCastTime { get; set; } = new(true);

    [Menu("Extra margin (ms)")]
    public RangeNode<int> CastTimeMarginMs { get; set; } = new(50, 0, 500);

    [Menu("Fixed gap between casts (ms)", "Fallback for when the cast time cannot be read")]
    public RangeNode<int> CastCooldownMs { get; set; } = new(400, 50, 3000);

    [Menu("Wait for the skill to be ready", "Checks the game instead of pressing and hoping")]
    public ToggleNode RequireSkillReady { get; set; } = new(true);

    [Menu("Flame Link internal name", "Shown in the discovery window's skill list")]
    public TextNode SkillInternalName { get; set; } = new("flame_link");

    [Menu("Max cast distance", "0 for no limit")]
    public RangeNode<float> MaxCastDistance { get; set; } = new(100f, 0f, 1000f);

    [Menu("Ignore other people's links", "Another player's link puts the same buff on your minion")]
    public ToggleNode OnlyMyLinks { get; set; } = new(true);

    [Menu("Count a link as gone below (s)", "Re-links just before it runs out. 0 turns this off")]
    public RangeNode<float> TreatAsUnlinkedBelowSeconds { get; set; } = new(0f, 0f, 10f);

    [Menu("Second ring when unlinked", "Easier to spot mid fight than a colour change")]
    public ToggleNode DrawInnerRingWhenUnlinked { get; set; } = new(true);

    [Menu("Move the cursor back afterwards")]
    public ToggleNode RestoreCursor { get; set; } = new(true);

    [Menu("Cursor settle time (ms)", "Too low and the skill fires at wherever the cursor used to be")]
    public RangeNode<int> CursorSettleMs { get; set; } = new(60, 10, 300);

    [Menu("Hold the cursor after pressing (ms)")]
    public RangeNode<int> CursorRestoreDelayMs { get; set; } = new(40, 0, 300);
}

[Submenu(CollapsedByDefault = true)]
public class RecallSkillSettings
{
    [Menu("Enable")]
    public ToggleNode Enable { get; set; } = new(false);

    [Menu("Skill key", "The key this skill sits on in game")]
    public HotkeyNodeV2 SkillKey { get; set; } = new(Keys.None);

    [Menu("Recall when one gets low")]
    public ToggleNode UseHealthTrigger { get; set; } = new(false);

    [Menu("Life threshold (%)")]
    public RangeNode<int> HealthThresholdPercent { get; set; } = new(50, 1, 99);

    [Menu("Recall when one strays too far")]
    public ToggleNode UseDistanceTrigger { get; set; } = new(false);

    [Menu("Distance threshold")]
    public RangeNode<float> DistanceThreshold { get; set; } = new(120f, 10f, 1000f);

    [Menu("Match the skill's cooldown", "Reads the real cooldown, which already includes your recovery")]
    public ToggleNode UseSkillCooldown { get; set; } = new(true);

    [Menu("Extra margin (ms)")]
    public RangeNode<int> CooldownMarginMs { get; set; } = new(100, 0, 1000);

    [Menu("Fixed gap between recalls (ms)", "Fallback for when the cooldown cannot be read")]
    public RangeNode<int> CooldownMs { get; set; } = new(1000, 100, 10000);

    [Menu("Wait for the skill to be ready")]
    public ToggleNode RequireSkillReady { get; set; } = new(true);

    [Menu("Skill internal name", "Shown in the discovery window's skill list")]
    public TextNode SkillInternalName { get; set; } = new("");

    public static RecallSkillSettings ForMercenary() => new()
    {
        SkillInternalName = new TextNode("call_mercenary"),
    };

    public static RecallSkillSettings ForConvocation() => new()
    {
        SkillInternalName = new TextNode("convocation"),
    };
}

[Submenu(CollapsedByDefault = true)]
public class DisplaySettings
{
    [Menu("Show in town and hideout")]
    public ToggleNode DrawInTown { get; set; } = new(false);

    [Menu("Hide while a panel is open", "Keeps the circles off your stash and inventory")]
    public ToggleNode HideWithPanelsOpen { get; set; } = new(true);

    [Menu("Only show minions I picked", "Follows the Link targets list")]
    public ToggleNode OnlyDrawPicked { get; set; } = new(false);

    [Menu("Show names")]
    public ToggleNode ShowLabels { get; set; } = new(false);

    [Menu("Max draw distance", "0 for no limit")]
    public RangeNode<float> MaxDistance { get; set; } = new(0f, 0f, 2000f);

    [Menu("Bend circles over terrain")]
    public ToggleNode FollowTerrain { get; set; } = new(true);

    [Menu("Circle smoothness")]
    public RangeNode<int> SegmentCount { get; set; } = new(32, 8, 128);
}

[Submenu(CollapsedByDefault = true)]
public class SafetySettings
{
    [Menu("Never act in town or hideout")]
    public ToggleNode DontCastInTown { get; set; } = new(true);

    [Menu("Never act with a panel open", "Stash, inventory and other full screen panels")]
    public ToggleNode DontCastWithPanelsOpen { get; set; } = new(true);
}

[Submenu(CollapsedByDefault = true)]
public class AdvancedSettings
{
    [Menu("Scan allied monsters too",
        "Last resort. It cannot tell your minions from anyone else's, so it will also circle " +
        "reserve mercenaries in town. Only worth it if a minion is never found")]
    public ToggleNode ScanAlliedMonsters { get; set; } = new(false);

    [Menu("Rescan every (ms)", "0 rebuilds the list every frame")]
    public RangeNode<int> RescanIntervalMs { get; set; } = new(100, 0, 1000);
}

[Submenu(CollapsedByDefault = true)]
public class DiscoverySettings
{
    [Menu("Show discovery window", "Lists what the detector saw and every skill you have, with internal names")]
    public ToggleNode ShowWindow { get; set; } = new(false);

    [Menu("Toggle key")]
    public HotkeyNodeV2 ToggleWindowHotkey { get; set; } = new(Keys.None);

    [Menu("Hide entities that matched", "Leaves only what is not being recognised")]
    public ToggleNode OnlyUnmatched { get; set; } = new(false);

    [JsonIgnore]
    [Menu("Copy list to clipboard")]
    public ButtonNode CopyToClipboard { get; set; } = new();
}
