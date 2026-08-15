using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using ExileCore;
using ExileCore.Shared.Helpers;
using LuminaryHelper.Casting;
using LuminaryHelper.Minions;
using LuminaryHelper.Settings;

namespace LuminaryHelper.Linking;

public sealed class FlameLinkCaster(
    GameController gameController,
    CastGuard castGuard,
    SkillReadiness skillReadiness,
    LuminaryHelperSettings settings)
{
    private enum Stage
    {
        Idle,
        WaitingForCursor,
        WaitingToRestoreCursor,
    }

    private readonly Stopwatch _sinceStageStart = Stopwatch.StartNew();
    private readonly Stopwatch _sinceLastCast = Stopwatch.StartNew();

    private Stage _stage = Stage.Idle;
    private Vector2 _cursorBeforeCast;

    public string LastBlockReason { get; private set; } = "Disabled";
    public string LastTargetName { get; private set; } = "";
    public int CastCount { get; private set; }
    public bool IsCasting => _stage != Stage.Idle;

    public void Update(IReadOnlyList<TrackedMinion> minions)
    {
        if (_stage != Stage.Idle)
        {
            AdvanceInFlightCast();
            return;
        }

        if (!CanCast(out var reason))
        {
            LastBlockReason = reason;
            return;
        }

        var target = ChooseTarget(minions);
        if (target == null)
        {
            LastBlockReason = "Nothing to link";
            return;
        }

        LastBlockReason = "Ready";
        BeginCast(target);
    }

    public int EffectiveCooldownMs
    {
        get
        {
            var tuning = settings.FlameLinkTuning;
            if (!tuning.UseSkillCastTime) return tuning.CastCooldownMs;

            if (!skillReadiness.TryFind(tuning.SkillInternalName.Value, out var skill))
                return tuning.CastCooldownMs;

            var castMs = (int)skill.CastTime.TotalMilliseconds;
            return castMs > 0 ? castMs + tuning.CastTimeMarginMs : tuning.CastCooldownMs.Value;
        }
    }

    private TrackedMinion ChooseTarget(IReadOnlyList<TrackedMinion> minions)
    {
        var maxDistance = settings.FlameLinkTuning.MaxCastDistance.Value;

        return minions
            .Where(m => !m.IsLinked)
            .Where(m => m.Entity is { IsValid: true, IsAlive: true })
            .Where(m => settings.ForKind(m.Kind).AutoLink)
            .Where(m => MinionIdentity.IsPicked(settings, m.Entity))
            .Where(m => maxDistance <= 0 || m.Entity.DistancePlayer <= maxDistance)
            .Where(m => TryGetScreenPosition(m, out _))
            .OrderBy(m => m.Entity.DistancePlayer)
            .FirstOrDefault();
    }

    private void BeginCast(TrackedMinion target)
    {
        if (!TryGetScreenPosition(target, out var screenPosition)) return;

        _cursorBeforeCast = Input.MousePositionNum;
        LastTargetName = target.Entity.RenderName ?? target.Kind.ToString();

        Input.SetCursorPos(WindowTopLeft() + screenPosition);

        _stage = Stage.WaitingForCursor;
        _sinceStageStart.Restart();
    }

    private void AdvanceInFlightCast()
    {
        var tuning = settings.FlameLinkTuning;

        switch (_stage)
        {
            case Stage.WaitingForCursor:
                if (_sinceStageStart.ElapsedMilliseconds < tuning.CursorSettleMs) return;

                InputHelper.SendInputPress(settings.FlameLink.SkillKey.Value);
                CastCount++;
                _sinceLastCast.Restart();

                _stage = Stage.WaitingToRestoreCursor;
                _sinceStageStart.Restart();
                return;

            case Stage.WaitingToRestoreCursor:
                if (_sinceStageStart.ElapsedMilliseconds < tuning.CursorRestoreDelayMs) return;

                if (tuning.RestoreCursor) Input.SetCursorPos(_cursorBeforeCast);

                _stage = Stage.Idle;
                return;
        }
    }

    private bool CanCast(out string reason)
    {
        var flameLink = settings.FlameLink;

        if (!flameLink.AutoCast)
        {
            reason = "Auto cast is off";
            return false;
        }

        if (flameLink.SkillKey.Value?.Key is null or Keys.None)
        {
            reason = "No Flame Link key set";
            return false;
        }

        if (flameLink.RequireHotkeyHeld)
        {
            if (flameLink.CastHotkey.Value?.Key is null or Keys.None)
            {
                reason = "No hold key set";
                return false;
            }

            if (!IsCastHotkeyHeld())
            {
                reason = "Hold key is not down";
                return false;
            }
        }

        if (_sinceLastCast.ElapsedMilliseconds < EffectiveCooldownMs)
        {
            reason = "Waiting for cast cooldown";
            return false;
        }

        if (!castGuard.CanSendInput(out var blockReason))
        {
            reason = blockReason;
            return false;
        }

        if (settings.FlameLinkTuning.RequireSkillReady &&
            !skillReadiness.IsReady(settings.FlameLinkTuning.SkillInternalName.Value))
        {
            reason = "Flame Link is not ready";
            return false;
        }

        reason = "Ready";
        return true;
    }

    private bool IsCastHotkeyHeld()
    {
        var key = settings.FlameLink.CastHotkey.Value?.Key;
        return key is { } pressed && Input.IsKeyDown((int)pressed);
    }

    private bool TryGetScreenPosition(TrackedMinion minion, out Vector2 screenPosition)
    {
        screenPosition = default;

        var entity = minion.Entity;
        if (entity is not { IsValid: true }) return false;

        var worldPosition = gameController.IngameState.Data.ToWorldWithTerrainHeight(entity.GridPosNum);
        screenPosition = gameController.IngameState.Camera.WorldToScreen(worldPosition);

        var window = gameController.Window.GetWindowRectangleTimeCache;
        return screenPosition.X > 0 && screenPosition.Y > 0 &&
               screenPosition.X < window.Width && screenPosition.Y < window.Height;
    }

    private Vector2 WindowTopLeft() =>
        gameController.Window.GetWindowRectangleTimeCache.TopLeft.ToVector2Num();
}
