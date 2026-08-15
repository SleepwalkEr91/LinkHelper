using System.Collections.Generic;
using ExileCore;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using LuminaryHelper.Casting;
using LuminaryHelper.Minions;
using LuminaryHelper.Settings;
using SharpDX;

namespace LuminaryHelper.Rendering;

public sealed class MinionCircleRenderer(
    GameController gameController,
    Graphics graphics,
    CastGuard castGuard,
    LuminaryHelperSettings settings)
{
    public void Draw(IReadOnlyList<TrackedMinion> minions)
    {
        var display = settings.Display;

        if (minions.Count == 0) return;
        if (!display.DrawInTown && castGuard.IsInTownOrHideout()) return;
        if (display.HideWithPanelsOpen && castGuard.AnyPanelOpen) return;

        var maxDistance = display.MaxDistance.Value;
        var segmentCount = display.SegmentCount.Value;
        var followTerrain = display.FollowTerrain.Value;
        var showLabels = display.ShowLabels.Value;

        foreach (var minion in minions)
        {
            var entity = minion.Entity;
            if (entity is not { IsValid: true }) continue;
            if (maxDistance > 0 && entity.DistancePlayer > maxDistance) continue;

            var kindSettings = settings.ForKind(minion.Kind);
            if (!kindSettings.Enable) continue;
            if (display.OnlyDrawPicked && !MinionIdentity.IsPicked(settings, entity)) continue;

            var groundPosition = gameController.IngameState.Data.ToWorldWithTerrainHeight(entity.GridPosNum);
            var showAsUnlinked = settings.FlameLink.TrackLinkState && !minion.IsLinked;
            var color = showAsUnlinked ? settings.FlameLink.UnlinkedColor.Value : kindSettings.Color.Value;
            var radius = kindSettings.Radius.Value;
            var thickness = kindSettings.Thickness.Value;

            graphics.DrawCircleInWorld(groundPosition, radius, color, thickness, segmentCount, followTerrain);

            if (showAsUnlinked && settings.FlameLinkTuning.DrawInnerRingWhenUnlinked)
                graphics.DrawCircleInWorld(groundPosition, radius * 0.6f, color, thickness, segmentCount, followTerrain);

            if (!showLabels) continue;

            var screenPosition = gameController.IngameState.Camera.WorldToScreen(groundPosition);
            graphics.DrawTextWithBackground(entity.RenderName, screenPosition, color, FontAlign.Center, Color.Black);
        }
    }
}
