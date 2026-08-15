using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;

namespace LuminaryHelper.Settings;

[Submenu(CollapsedByDefault = false)]
public class MinionKindSettings
{
    [Menu("Track this type")]
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Let Flame Link target it")]
    public ToggleNode AutoLink { get; set; } = new(true);

    [Menu("Circle colour")]
    public ColorNode Color { get; set; } = new(SharpDX.Color.White);

    [Menu("Circle size", "Roughly 11 units to a grid tile")]
    public RangeNode<float> Radius { get; set; } = new(50f, 5f, 300f);

    [Menu("Line width")]
    public RangeNode<float> Thickness { get; set; } = new(2f, 1f, 10f);

    [Menu("Summoned by", "Skill internal name. Comma separated, partial matches count")]
    public TextNode SkillPatterns { get; set; } = new("");

    [Menu("Metadata contains", "Comma separated, partial matches count")]
    public TextNode MetadataPatterns { get; set; } = new("");

    public static MinionKindSettings ForSpectres() => new()
    {
        Color = new ColorNode(SharpDX.Color.DeepSkyBlue),
        SkillPatterns = new TextNode("raise_spectre"),
    };

    public static MinionKindSettings ForMercenaries() => new()
    {
        Color = new ColorNode(SharpDX.Color.Orange),
        MetadataPatterns = new TextNode("Metadata/Monsters/Mercenaries/"),
    };

    public static MinionKindSettings ForAnimateGuardian() => new()
    {
        Color = new ColorNode(SharpDX.Color.Gold),
        SkillPatterns = new TextNode("animate_guardian"),
        MetadataPatterns = new TextNode("Metadata/Monsters/AnimatedItem/AnimatedArmour"),
    };
}
