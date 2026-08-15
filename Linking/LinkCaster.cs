using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using ExileCore;
using ExileCore.Shared.Helpers;
using LinkHelper.Casting;
using LinkHelper.Players;
using LinkHelper.Settings;

namespace LinkHelper.Linking;

public sealed class LinkCaster(
    GameController gameController,
    CastGuard castGuard,
    SkillReadiness skillReadiness,
    LinkHelperSettings settings,
    LinkCastHistory castHistory)
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

    public void Update(IReadOnlyList<TrackedPlayer> players)
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

        var target = ChooseTarget(players);
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
            var tuning = settings.LinkTuning;
            if (!tuning.UseSkillCastTime) return tuning.CastCooldownMs;

            if (!skillReadiness.TryFind(settings.Link.SkillInternalName.Value, out var skill))
                return tuning.CastCooldownMs;

            var castMs = (int)skill.CastTime.TotalMilliseconds;
            return castMs > 0 ? castMs + tuning.CastTimeMarginMs : tuning.CastCooldownMs.Value;
        }
    }

    private TrackedPlayer ChooseTarget(IReadOnlyList<TrackedPlayer> players)
    {
        var maxDistance = settings.LinkTuning.MaxCastDistance.Value;

        return players
            .Where(p => !p.IsLinked)
            .Where(p => p.Entity is { IsValid: true, IsAlive: true })
            .Where(p => PlayerIdentity.IsPicked(settings, p.Entity))
            .Where(p => maxDistance <= 0 || p.Entity.DistancePlayer <= maxDistance)
            .Where(p => TryGetScreenPosition(p, out _))
            .OrderBy(p => p.Entity.DistancePlayer)
            .FirstOrDefault();
    }

    private void BeginCast(TrackedPlayer target)
    {
        if (!TryGetScreenPosition(target, out var screenPosition)) return;

        _cursorBeforeCast = Input.MousePositionNum;
        LastTargetName = PlayerIdentity.KeyFor(target.Entity);

        Input.SetCursorPos(WindowTopLeft() + screenPosition);

        _stage = Stage.WaitingForCursor;
        _sinceStageStart.Restart();
    }

    private void AdvanceInFlightCast()
    {
        var tuning = settings.LinkTuning;

        switch (_stage)
        {
            case Stage.WaitingForCursor:
                if (_sinceStageStart.ElapsedMilliseconds < tuning.CursorSettleMs) return;

                InputHelper.SendInputPress(settings.Link.SkillKey.Value);
                CastCount++;
                _sinceLastCast.Restart();
                castHistory.RecordCast(LastTargetName);

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
        var link = settings.Link;

        if (!link.AutoCast)
        {
            reason = "Auto cast is off";
            return false;
        }

        if (link.SkillKey.Value?.Key is null or Keys.None)
        {
            reason = "No Link key set";
            return false;
        }

        if (link.RequireHotkeyHeld)
        {
            if (link.CastHotkey.Value?.Key is null or Keys.None)
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

        if (settings.LinkTuning.RequireSkillReady &&
            !skillReadiness.IsReady(settings.Link.SkillInternalName.Value))
        {
            reason = "Link skill is not ready";
            return false;
        }

        reason = "Ready";
        return true;
    }

    private bool IsCastHotkeyHeld()
    {
        var key = settings.Link.CastHotkey.Value?.Key;
        return key is { } pressed && Input.IsKeyDown((int)pressed);
    }

    private bool TryGetScreenPosition(TrackedPlayer player, out Vector2 screenPosition)
    {
        screenPosition = default;

        var entity = player.Entity;
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
