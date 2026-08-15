using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using LinkHelper.Linking;
using Newtonsoft.Json;

namespace LinkHelper.Settings;

public class LinkHelperSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(false);

    [Menu("Link", 100)]
    public LinkSettings Link { get; set; } = new();

    [Menu("Link tuning", 110)]
    public LinkTuning LinkTuning { get; set; } = new();

    [Menu("Display", 200)]
    public DisplaySettings Display { get; set; } = new();

    [Menu("Safety", 300)]
    public SafetySettings Safety { get; set; } = new();

    [Menu("Advanced", 400)]
    public AdvancedSettings Advanced { get; set; } = new();

    [Menu("Discovery", 500)]
    public DiscoverySettings Discovery { get; set; } = new();
}

[Submenu(CollapsedByDefault = false)]
public class LinkSettings
{
    [Menu("Highlight unlinked players",
        "Draws a player in the colour below while your link is missing from them")]
    public ToggleNode TrackLinkState { get; set; } = new(true);

    [Menu("Unlinked colour")]
    public ColorNode UnlinkedColor { get; set; } = new(SharpDX.Color.Red);

    [Menu("Cast link for me", "Everything below stays idle until this is on and a key is set")]
    public ToggleNode AutoCast { get; set; } = new(true);

    [Menu("Link key", "The key the link skill sits on in game")]
    public HotkeyNodeV2 SkillKey { get; set; } = new(Keys.None);

    [Menu("Only while I hold a key", "Off means it re-links on its own")]
    public ToggleNode RequireHotkeyHeld { get; set; } = new(false);

    [Menu("Hold key", "Never sent to the game, only read")]
    public HotkeyNodeV2 CastHotkey { get; set; } = new(Keys.None);

    [Menu("Only link players I picked", "Uses the Link targets list at the top. Off links every player nearby")]
    public ToggleNode LinkOnlySelected { get; set; } = new(false);

    [Menu("Link preset", "Pick an existing link skill, or \"Custom\" to define your own")]
    public ListNode LinkPreset { get; set; } = new() { Value = "Flame Link" };

    [ConditionalDisplay(nameof(IsCustomLinkPreset))]
    [Menu("Link internal name", "Shown in the discovery window's skill list. Matches whichever " +
        "preset you picked above, or fill in your own")]
    public TextNode SkillInternalName { get; set; } = new("flame_link");

    [ConditionalDisplay(nameof(IsCustomLinkPreset))]
    [Menu("Link buff on you (source)",
        "Buff you carry on yourself while a link is active. Shown in the status line and the " +
        "discovery window as extra info only - it does not affect who counts as linked below. " +
        "Leave empty to hide that line")]
    public TextNode SourceBuffPattern { get; set; } = new("flame_link_source");

    [ConditionalDisplay(nameof(IsCustomLinkPreset))]
    [Menu("Link buff on target", "Name of the buff a link puts on the linked player")]
    public TextNode TargetBuffPattern { get; set; } = new("flame_link_target");

    [IgnoreMenu]
    public Dictionary<string, bool> SelectedTargets { get; set; } = new();

    /// <summary>
    /// Condition for ConditionalDisplay above: only show the three manual fields while the
    /// dropdown is not pointing at one of our known presets - "Custom", or anything typed in
    /// that we don't recognise, both count as "fill it in by hand".
    /// </summary>
    public bool IsCustomLinkPreset() => !LinkPresets.All.Any(p => p.DisplayName == LinkPreset.Value);
}

[Submenu(CollapsedByDefault = true)]
public class LinkTuning
{
    [Menu("Match the skill's cast time", "Paces casts to the link skill's real cast time instead of the fixed gap below")]
    public ToggleNode UseSkillCastTime { get; set; } = new(true);

    [Menu("Extra margin (ms)")]
    public RangeNode<int> CastTimeMarginMs { get; set; } = new(50, 0, 500);

    [Menu("Fixed gap between casts (ms)", "Fallback for when the cast time cannot be read")]
    public RangeNode<int> CastCooldownMs { get; set; } = new(400, 50, 3000);

    [Menu("Wait for the skill to be ready", "Checks the game instead of pressing and hoping")]
    public ToggleNode RequireSkillReady { get; set; } = new(true);

    [Menu("Max cast distance", "0 for no limit")]
    public RangeNode<float> MaxCastDistance { get; set; } = new(700f, 0f, 1000f);

    [Menu("Ignore other people's links", "Someone else linking the same target puts the same buff on them")]
    public ToggleNode OnlyMyLinks { get; set; } = new(true);

    [Menu("Link duration override (s)",
        "How long the link buff lasts. 0 uses the duration read off the skill itself; any other " +
        "value overrides that")]
    public RangeNode<float> LinkDurationOverrideSeconds { get; set; } = new(0f, 0f, 60f);

    [Menu("Re-link this many seconds early", "How long before the known duration above to cast again")]
    public RangeNode<float> RelinkMarginSeconds { get; set; } = new(1f, 0f, 10f);

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
public class DisplaySettings
{
    [Menu("Circle colour", "Used for linked players, or for any player while link tracking is off")]
    public ColorNode Color { get; set; } = new(SharpDX.Color.White);

    [Menu("Circle size", "Roughly 11 units to a grid tile")]
    public RangeNode<float> Radius { get; set; } = new(50f, 5f, 300f);

    [Menu("Line width")]
    public RangeNode<float> Thickness { get; set; } = new(2f, 1f, 10f);

    [Menu("Show in town and hideout")]
    public ToggleNode DrawInTown { get; set; } = new(false);

    [Menu("Hide while a panel is open", "Keeps the circles off your stash and inventory")]
    public ToggleNode HideWithPanelsOpen { get; set; } = new(true);

    [Menu("Only show players I picked", "Follows the Link targets list")]
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
    [Menu("Rescan every (ms)", "0 rebuilds the list every frame")]
    public RangeNode<int> RescanIntervalMs { get; set; } = new(100, 0, 1000);
}

[Submenu(CollapsedByDefault = true)]
public class DiscoverySettings
{
    [Menu("Show discovery window", "Lists every player nearby and every skill you have, with internal names")]
    public ToggleNode ShowWindow { get; set; } = new(false);

    [Menu("Toggle key")]
    public HotkeyNodeV2 ToggleWindowHotkey { get; set; } = new(Keys.None);

    [JsonIgnore]
    [Menu("Copy list to clipboard")]
    public ButtonNode CopyToClipboard { get; set; } = new();
}
