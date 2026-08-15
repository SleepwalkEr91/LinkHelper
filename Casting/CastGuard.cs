using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using LinkHelper.Settings;

namespace LinkHelper.Casting;

public sealed class CastGuard(GameController gameController, LinkHelperSettings settings)
{
    public bool CanSendInput(out string reason)
    {
        if (!gameController.Window.IsForeground())
        {
            reason = "Game window is not focused";
            return false;
        }

        if (gameController.Game.IsEscapeState)
        {
            reason = "Escape menu is open";
            return false;
        }

        if (!gameController.Player.TryGetComponent<Life>(out var life) || life == null || life.CurHP <= 0)
        {
            reason = "Player is dead";
            return false;
        }

        if (gameController.Player.TryGetComponent<Buffs>(out var buffs) &&
            buffs != null && buffs.HasBuff("grace_period"))
        {
            reason = "Grace period is active";
            return false;
        }

        if (settings.Safety.DontCastInTown && IsInTownOrHideout())
        {
            reason = "In town or hideout";
            return false;
        }

        if (gameController.IngameState.FocusedInputElement != null)
        {
            reason = "A text field has focus";
            return false;
        }

        if (gameController.IngameState.IngameUi.ChatTitlePanel?.IsVisible == true)
        {
            reason = "Chat is open";
            return false;
        }

        if (settings.Safety.DontCastWithPanelsOpen && AnyPanelOpen)
        {
            reason = "A panel is open";
            return false;
        }

        reason = "Ready";
        return true;
    }

    public bool AnyPanelOpen
    {
        get
        {
            var ingameUi = gameController.IngameState.IngameUi;

            return ingameUi.OpenLeftPanel?.IsVisible == true ||
                   ingameUi.OpenRightPanel?.IsVisible == true ||
                   ingameUi.LargePanels?.Any(panel => panel.IsVisible) == true ||
                   ingameUi.FullscreenPanels?.Any(panel => panel.IsVisible) == true;
        }
    }

    public bool IsInTownOrHideout()
    {
        var area = gameController.Area?.CurrentArea;
        return area is { IsTown: true } or { IsHideout: true };
    }
}
