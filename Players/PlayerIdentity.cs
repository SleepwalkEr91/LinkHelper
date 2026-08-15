using System.Collections.Generic;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using LinkHelper.Settings;

namespace LinkHelper.Players;

public static class PlayerIdentity
{
    // NOTE: not 100% verified - the "Player" component's name property is assumed to be
    // "PlayerName" based on what you described from the dev tree. If the build fails here,
    // check the actual property name on ExileCore.PoEMemory.Components.Player and swap it in.
    public static string KeyFor(Entity entity)
    {
        if (entity == null) return "";

        if (entity.TryGetComponent<Player>(out var player) && player != null &&
            !string.IsNullOrWhiteSpace(player.PlayerName))
            return player.PlayerName;

        return entity.RenderName ?? entity.Metadata ?? "";
    }

    public static bool IsPicked(LinkHelperSettings settings, Entity entity)
    {
        if (!settings.Link.LinkOnlySelected) return true;

        var key = KeyFor(entity);
        return !string.IsNullOrEmpty(key) && settings.Link.SelectedTargets.GetValueOrDefault(key);
    }
}
