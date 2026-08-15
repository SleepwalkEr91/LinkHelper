using System.Collections.Generic;
using ExileCore;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using LinkHelper.Casting;
using LinkHelper.Players;
using LinkHelper.Settings;
using SharpDX;

namespace LinkHelper.Rendering;

public sealed class PlayerCircleRenderer(
    GameController gameController,
    Graphics graphics,
    CastGuard castGuard,
    LinkHelperSettings settings)
{
    public void Draw(IReadOnlyList<TrackedPlayer> players)
    {
        var display = settings.Display;

        if (players.Count == 0) return;
        if (!display.DrawInTown && castGuard.IsInTownOrHideout()) return;
        if (display.HideWithPanelsOpen && castGuard.AnyPanelOpen) return;

        var maxDistance = display.MaxDistance.Value;
        var segmentCount = display.SegmentCount.Value;
        var followTerrain = display.FollowTerrain.Value;
        var showLabels = display.ShowLabels.Value;

        foreach (var player in players)
        {
            var entity = player.Entity;
            if (entity is not { IsValid: true }) continue;
            if (maxDistance > 0 && entity.DistancePlayer > maxDistance) continue;
            if (display.OnlyDrawPicked && !PlayerIdentity.IsPicked(settings, entity)) continue;

            var groundPosition = gameController.IngameState.Data.ToWorldWithTerrainHeight(entity.GridPosNum);
            var showAsUnlinked = settings.Link.TrackLinkState && !player.IsLinked;
            var color = showAsUnlinked ? settings.Link.UnlinkedColor.Value : display.Color.Value;
            var radius = display.Radius.Value;
            var thickness = display.Thickness.Value;

            graphics.DrawCircleInWorld(groundPosition, radius, color, thickness, segmentCount, followTerrain);

            if (showAsUnlinked && settings.LinkTuning.DrawInnerRingWhenUnlinked)
                graphics.DrawCircleInWorld(groundPosition, radius * 0.6f, color, thickness, segmentCount, followTerrain);

            if (!showLabels) continue;

            var screenPosition = gameController.IngameState.Camera.WorldToScreen(groundPosition);
            var label = PlayerIdentity.KeyFor(entity);
            graphics.DrawTextWithBackground(label, screenPosition, color, FontAlign.Center, Color.Black);
        }
    }
}
