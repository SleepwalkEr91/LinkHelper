using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ImGuiNET;
using LinkHelper.Casting;
using LinkHelper.Diagnostics;
using LinkHelper.Linking;
using LinkHelper.Players;
using LinkHelper.Rendering;
using LinkHelper.Settings;

namespace LinkHelper;

public class LinkHelperPlugin : BaseSettingsPlugin<LinkHelperSettings>
{
    private static readonly Vector4 WarningColor = new(1f, 0.8f, 0.3f, 1f);

    private readonly Stopwatch _sinceLastScan = Stopwatch.StartNew();

    private PlayerTracker _tracker;
    private PlayerCircleRenderer _renderer;
    private PlayerDiscoveryPanel _discoveryPanel;
    private LinkCaster _linkCaster;
    private LinkStateEvaluator _linkStateEvaluator;
    private SkillReadiness _skillReadiness;

    public override bool Initialise()
    {
        var castGuard = new CastGuard(GameController, Settings);
        var castHistory = new LinkCastHistory();
        _skillReadiness = new SkillReadiness(GameController);
        _linkStateEvaluator = new LinkStateEvaluator(GameController, Settings, castHistory, _skillReadiness);

        _tracker = new PlayerTracker(GameController, Settings, _linkStateEvaluator);
        _renderer = new PlayerCircleRenderer(GameController, Graphics, castGuard, Settings);
        _discoveryPanel = new PlayerDiscoveryPanel(Settings);
        _linkCaster = new LinkCaster(GameController, castGuard, _skillReadiness, Settings, castHistory, _linkStateEvaluator);

        RegisterDiscoveryHotkey();
        RegisterCastHotkey();
        Settings.Discovery.ToggleWindowHotkey.OnValueChanged += RegisterDiscoveryHotkey;
        Settings.Link.CastHotkey.OnValueChanged += RegisterCastHotkey;
        Settings.Discovery.CopyToClipboard.OnPressed += CopyDiscoveryReport;

        Settings.Link.LinkPreset.SetListValues(BuildLinkPresetOptions());
        Settings.Link.LinkPreset.AllowCustomValues = true;
        Settings.Link.LinkPreset.OnValueSelected = ApplyLinkPresetByName;

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _tracker?.Refresh(collectCandidates: false);
    }

    public override Job Tick()
    {
        if (Settings.Discovery.ToggleWindowHotkey.PressedOnce())
            Settings.Discovery.ShowWindow.Value = !Settings.Discovery.ShowWindow;

        if (!ShouldRun()) return null;

        // Every tick, not gated by RescanIntervalMs - the decay-rate estimate needs real,
        // frequent samples to stay accurate, independent of how often the player list itself
        // gets rebuilt.
        _linkStateEvaluator.UpdateBuffDecayRate();

        var interval = Settings.Advanced.RescanIntervalMs.Value;
        if (interval <= 0 || _sinceLastScan.ElapsedMilliseconds >= interval)
        {
            _sinceLastScan.Restart();
            _tracker.Refresh(collectCandidates: Settings.Discovery.ShowWindow);
        }

        _linkCaster.Update(_tracker.Players);

        return null;
    }

    public override void Render()
    {
        if (!ShouldRun()) return;

        _renderer.Draw(_tracker.Players);

        var skills = Settings.Discovery.ShowWindow ? _skillReadiness.DescribeAll() : [];
        _discoveryPanel.Draw(_tracker.Candidates, skills, _tracker.LastError, _linkStateEvaluator.HasActiveSourceBuff());
    }

    public override void DrawSettings()
    {
        ImGui.TextUnformatted(BuildStatusLine());
        ImGui.Separator();
        DrawLinkTargetPicker();
        ImGui.Separator();
        base.DrawSettings();
    }

    private void DrawLinkTargetPicker()
    {
        if (!ImGui.CollapsingHeader("Link targets", ImGuiTreeNodeFlags.DefaultOpen)) return;

        var picked = Settings.Link.SelectedTargets;

        var present = new HashSet<string>();
        foreach (var player in _tracker.Players)
        {
            var key = PlayerIdentity.KeyFor(player.Entity);
            if (!string.IsNullOrEmpty(key)) present.Add(key);
        }

        var rows = present.Union(picked.Keys).OrderBy(key => key).ToList();

        if (rows.Count == 0)
        {
            ImGui.TextDisabled("No players seen yet.");
            return;
        }

        if (!Settings.Link.LinkOnlySelected)
            ImGui.TextDisabled("Every player nearby is being linked. Turn on \"Only link players I picked\" to use this list.");
        else if (!picked.Any(entry => entry.Value))
            ImGui.TextColored(WarningColor, "Nothing is ticked, so nothing will be linked.");

        foreach (var key in rows)
        {
            ImGui.PushID(key);

            var isPicked = picked.GetValueOrDefault(key);
            if (ImGui.Checkbox(key, ref isPicked)) picked[key] = isPicked;

            ImGui.SameLine();
            ImGui.TextDisabled(present.Contains(key) ? "(here now)" : "(not seen)");

            ImGui.PopID();
        }

        if (ImGui.Button("Forget players that are not here"))
            foreach (var key in picked.Keys.Where(k => !present.Contains(k)).ToList())
                picked.Remove(key);
    }

    private string BuildStatusLine()
    {
        if (!GameController.InGame) return "Not in game";

        var status = $"Tracking - players nearby: {_tracker.Players.Count}";
        status += $"   |   missing link: {_tracker.Players.Count(p => !p.IsLinked)}";
        status += $"\nYour source buff: {(_linkStateEvaluator.HasActiveSourceBuff() ? "active" : "missing")}" +
                  $"   |   buff speed: {_linkStateEvaluator.CurrentBuffDecayRate:0.00}x";

        if (Settings.Link.AutoCast)
        {
            var target = string.IsNullOrEmpty(_linkCaster.LastTargetName) ? "" : $" -> {_linkCaster.LastTargetName}";
            status += $"\nLink: {_linkCaster.LastBlockReason}{target}   |   " +
                      $"gap: {_linkCaster.EffectiveCooldownMs} ms   |   sent: {_linkCaster.CastCount}" +
                      $"   |   unconfirmed: {_linkCaster.UnconfirmedCastCount}";
        }

        return status;
    }

    private static List<string> BuildLinkPresetOptions() =>
        new List<string> { "Custom" }.Concat(LinkPresets.All.Select(p => p.DisplayName)).ToList();

    /// <summary>
    /// Fired by the "Link preset" dropdown with whatever is now selected - one of our own preset
    /// names, "Custom", or anything the user typed themselves (AllowCustomValues is on). Only a
    /// name that matches one of our presets does anything; everything else is left alone, since
    /// that just means the three fields below are being filled in by hand.
    /// </summary>
    private void ApplyLinkPresetByName(string name)
    {
        var preset = LinkPresets.All.FirstOrDefault(p => p.DisplayName == name);
        if (preset != null) ApplyLinkPreset(preset);
    }

    private void ApplyLinkPreset(LinkPreset preset)
    {
        Settings.Link.SkillInternalName.Value = preset.SkillInternalName;
        Settings.Link.SourceBuffPattern.Value = preset.SourceBuffPattern;
        Settings.Link.TargetBuffPattern.Value = preset.TargetBuffPattern;
    }

    private bool ShouldRun() =>
        Settings.Enable && GameController.InGame && GameController.Player is { IsValid: true };

    private void RegisterDiscoveryHotkey() =>
        Input.RegisterKey(Settings.Discovery.ToggleWindowHotkey.Value);

    private void RegisterCastHotkey() =>
        Input.RegisterKey(Settings.Link.CastHotkey.Value);

    private void CopyDiscoveryReport()
    {
        Settings.Discovery.ShowWindow.Value = true;
        _tracker.Refresh(collectCandidates: true);

        var report = string.Join('\n', _tracker.Candidates.Select(c => string.Join('\t',
            PlayerIdentity.KeyFor(c.Entity),
            $"{c.Entity.DistancePlayer:0}",
            c.IsLinked ? "yes" : "no",
            c.Buffs)));

        ImGui.SetClipboardText($"Name\tDistance\tLinked\tBuffs\n{report}");
    }
}
