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
    LinkCastHistory castHistory,
    LinkStateEvaluator linkStateEvaluator)
{
    // Confirming a cast reads the source buff's timer, which is only worth a fixed, short wait
    // after pressing the key rather than a tunable setting - not something a user should need to
    // fiddle with.
    private const int ConfirmationDelayMs = 200;

    private enum Stage
    {
        Idle,
        WaitingForCursor,
        WaitingToRestoreCursor,
        WaitingForConfirmation,
    }

    private readonly Stopwatch _sinceStageStart = Stopwatch.StartNew();
    private readonly Stopwatch _sinceLastCast = Stopwatch.StartNew();

    private Stage _stage = Stage.Idle;
    private Vector2 _cursorBeforeCast;
    private bool _hadSourceBuffBeforeCast;
    private float _sourceBuffSecondsBeforeCast;

    public string LastBlockReason { get; private set; } = "Disabled";
    public string LastTargetName { get; private set; } = "";
    public int CastCount { get; private set; }
    public int UnconfirmedCastCount { get; private set; }
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
        _hadSourceBuffBeforeCast = linkStateEvaluator.TryGetSourceBuffTimerSeconds(out _sourceBuffSecondsBeforeCast);

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

                _stage = Stage.WaitingToRestoreCursor;
                _sinceStageStart.Restart();
                return;

            case Stage.WaitingToRestoreCursor:
                if (_sinceStageStart.ElapsedMilliseconds < tuning.CursorRestoreDelayMs) return;

                if (tuning.RestoreCursor) Input.SetCursorPos(_cursorBeforeCast);

                _stage = Stage.WaitingForConfirmation;
                _sinceStageStart.Restart();
                return;

            case Stage.WaitingForConfirmation:
                if (_sinceStageStart.ElapsedMilliseconds < ConfirmationDelayMs) return;

                ConfirmCast();

                _stage = Stage.Idle;
                return;
        }
    }

    /// <summary>
    /// Only counts a cast as having actually landed - and only then tells LinkCastHistory the
    /// player is freshly relinked - once the source buff's own timer confirms it. Without this, a
    /// cast that never actually reached the target - out of the skill's real range, briefly off
    /// screen, whatever - still got recorded as a success purely because we pressed the key,
    /// which reset our own countdown to "just relinked" while the real buff on the target kept
    /// counting down toward its original, unextended expiry. That silently created a much longer
    /// gap than "Re-link this many seconds early" was supposed to allow - the target would only
    /// be picked up again once its buff had actually run out, not when it was merely about to.
    ///
    /// The main check is against the skill's own known duration, not just "did the timer go up
    /// compared to before this cast": a successful cast puts the source buff back at (very close
    /// to) its full duration, so requiring the post-cast value to be near that duration catches
    /// it even when the pre-cast value was already close to full - e.g. relinking a second player
    /// moments after a first successful cast, where a plain before/after jump can be too small to
    /// tell apart from noise. The tolerance accounts for the delay between the actual key press
    /// and this check (CursorRestoreDelayMs + ConfirmationDelayMs), during which a genuinely
    /// refreshed buff has already been counting back down for a bit.
    ///
    /// Only when the duration itself is not known yet (no override, and the skill's Stats
    /// dictionary hasn't reported one - see LinkStateEvaluator.ResolveDurationSeconds) does this
    /// fall back to the weaker relative signal: the timer is now higher than right before we
    /// pressed the key, since a plain countdown can only go down on its own between polls.
    ///
    /// If no source buff pattern is configured there is nothing to check, so this falls back to
    /// trusting the cast as before.
    /// </summary>
    private void ConfirmCast()
    {
        var hasSourcePattern = settings.Link.SourceBuffPattern.Value.Trim().Length > 0;
        if (!hasSourcePattern)
        {
            castHistory.RecordCast(LastTargetName);
            return;
        }

        var hasNow = linkStateEvaluator.TryGetSourceBuffTimerSeconds(out var secondsNow);
        var durationSeconds = linkStateEvaluator.ResolveDurationSeconds();

        bool confirmed;
        if (durationSeconds > 0)
        {
            var toleranceSeconds = (settings.LinkTuning.CursorRestoreDelayMs.Value + ConfirmationDelayMs) / 1000f + 0.25f;
            confirmed = hasNow && secondsNow >= durationSeconds - toleranceSeconds;
        }
        else
        {
            confirmed = hasNow && (!_hadSourceBuffBeforeCast || secondsNow > _sourceBuffSecondsBeforeCast + 0.05f);
        }

        if (confirmed)
        {
            castHistory.RecordCast(LastTargetName);
            return;
        }

        UnconfirmedCastCount++;
        LastBlockReason = "Last cast to " + LastTargetName + " was not confirmed - retrying";
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
