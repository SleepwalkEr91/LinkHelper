using System.Collections.Generic;

namespace LinkHelper.Linking;

/// <summary>
/// Known internal names for the link skills. Filled in from what you found in ExileAPI's
/// dev tree - flip between them with the buttons in the Link settings, or leave the text fields
/// on something else entirely for a different link skill. Note the skill's internal name and its
/// buff names don't share a word (protective_link's buffs are bulwark_link_source/target, not
/// protective_link_source/target) - each preset was given to us explicitly rather than derived
/// from a pattern, so don't assume a naming rule holds for any link skill not listed here.
/// </summary>
public sealed record LinkPreset(string DisplayName, string SkillInternalName, string SourceBuffPattern, string TargetBuffPattern);

public static class LinkPresets
{
    public static readonly LinkPreset FlameLink = new("Flame Link", "flame_link", "flame_link_source", "flame_link_target");
    public static readonly LinkPreset SoulLink = new("Soul Link", "soul_link", "soul_link_source", "soul_link_target");
    public static readonly LinkPreset ProtectiveLink = new("Protective Link", "protective_link", "bulwark_link_source", "bulwark_link_target");
    public static readonly LinkPreset IntuitiveLink = new("Intuitive Link", "intuitive_link", "trigger_link_source", "trigger_link_target");
    public static readonly LinkPreset VampiricLink = new("Vampiric Link", "vampiric_link", "remora_link_source", "remora_link_target");
    public static readonly LinkPreset DestructiveLink = new("Destructive Link", "destructive_link", "critical_link_source", "critical_link_target");

    /// <summary>
    /// All presets in one place - used to build the preset buttons, and will double as the
    /// source list for a dropdown if we switch to one later.
    /// </summary>
    public static readonly IReadOnlyList<LinkPreset> All =
    [
        FlameLink, SoulLink, ProtectiveLink, IntuitiveLink, VampiricLink, DestructiveLink
    ];
}
