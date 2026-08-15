using System.Collections.Generic;
using ExileCore.PoEMemory.MemoryObjects;
using LuminaryHelper.Settings;

namespace LuminaryHelper.Minions;

public static class MinionIdentity
{
    public static string KeyFor(Entity entity)
    {
        if (entity == null) return "";

        var name = entity.RenderName;
        return string.IsNullOrWhiteSpace(name) ? entity.Metadata ?? "" : name;
    }

    public static bool IsPicked(LuminaryHelperSettings settings, Entity entity)
    {
        if (!settings.FlameLink.LinkOnlySelected) return true;

        var key = KeyFor(entity);
        return !string.IsNullOrEmpty(key) && settings.FlameLink.SelectedTargets.GetValueOrDefault(key);
    }
}
