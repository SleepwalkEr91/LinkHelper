using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ImGuiNET;
using LuminaryHelper.Casting;
using LuminaryHelper.Diagnostics;
using LuminaryHelper.Linking;
using LuminaryHelper.Minions;
using LuminaryHelper.Recall;
using LuminaryHelper.Rendering;
using LuminaryHelper.Settings;

namespace LuminaryHelper;

public class LuminaryHelperPlugin : BaseSettingsPlugin<LuminaryHelperSettings>
{
    private static readonly Vector4 WarningColor = new(1f, 0.8f, 0.3f, 1f);

    private readonly Stopwatch _sinceLastScan = Stopwatch.StartNew();

    private MinionTracker _tracker;
    private MinionCircleRenderer _renderer;
    private MinionDiscoveryPanel _discoveryPanel;
    private FlameLinkCaster _linkCaster;
    private RecallCaster _recallCaster;
    private SkillReadiness _skillReadiness;

    public override bool Initialise()
    {
        var castGuard = new CastGuard(GameController, Settings);
        var classifier = new MinionClassifier(Settings);
        var linkStateEvaluator = new LinkStateEvaluator(GameController, Settings);

        _skillReadiness = new SkillReadiness(GameController);
        _tracker = new MinionTracker(GameController, Settings, classifier, linkStateEvaluator);
        _renderer = new MinionCircleRenderer(GameController, Graphics, castGuard, Settings);
        _discoveryPanel = new MinionDiscoveryPanel(Settings);
        _linkCaster = new FlameLinkCaster(GameController, castGuard, _skillReadiness, Settings);
        _recallCaster = new RecallCaster(castGuard, _skillReadiness, Settings);

        RegisterDiscoveryHotkey();
        RegisterCastHotkey();
        Settings.Discovery.ToggleWindowHotkey.OnValueChanged += RegisterDiscoveryHotkey;
        Settings.FlameLink.CastHotkey.OnValueChanged += RegisterCastHotkey;
        Settings.Discovery.CopyToClipboard.OnPressed += CopyDiscoveryReport;

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

        var interval = Settings.Advanced.RescanIntervalMs.Value;
        if (interval <= 0 || _sinceLastScan.ElapsedMilliseconds >= interval)
        {
            _sinceLastScan.Restart();
            _tracker.Refresh(collectCandidates: Settings.Discovery.ShowWindow);
        }

        _linkCaster.Update(_tracker.Minions);

        if (!_linkCaster.IsCasting) _recallCaster.Update(_tracker.Minions);

        return null;
    }

    public override void Render()
    {
        if (!ShouldRun()) return;

        _renderer.Draw(_tracker.Minions);

        var skills = Settings.Discovery.ShowWindow ? _skillReadiness.DescribeAll() : [];
        _discoveryPanel.Draw(_tracker.Candidates, skills, _tracker.LastError);
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
        if (!ImGui.CollapsingHeader("Link targets")) return;

        var picked = Settings.FlameLink.SelectedTargets;

        var present = new Dictionary<string, MinionKind>();
        foreach (var minion in _tracker.Minions)
        {
            var key = MinionIdentity.KeyFor(minion.Entity);
            if (!string.IsNullOrEmpty(key)) present[key] = minion.Kind;
        }

        var rows = present.Keys.Union(picked.Keys).OrderBy(key => key).ToList();

        if (rows.Count == 0)
        {
            ImGui.TextDisabled("No minions seen yet. Summon them and they will show up here.");
            return;
        }

        if (!Settings.FlameLink.LinkOnlySelected)
            ImGui.TextDisabled("Every minion is being linked. Turn on \"Only link minions I picked\" to use this list.");
        else if (!picked.Any(entry => entry.Value))
            ImGui.TextColored(WarningColor, "Nothing is ticked, so nothing will be linked.");

        foreach (var key in rows)
        {
            ImGui.PushID(key);

            var isPicked = picked.GetValueOrDefault(key);
            if (ImGui.Checkbox(key, ref isPicked)) picked[key] = isPicked;

            ImGui.SameLine();
            ImGui.TextDisabled(present.TryGetValue(key, out var kind) ? $"({kind}, here now)" : "(not summoned)");

            ImGui.PopID();
        }

        if (ImGui.Button("Forget minions that are not here"))
            foreach (var key in picked.Keys.Where(k => !present.ContainsKey(k)).ToList())
                picked.Remove(key);
    }

    private string BuildStatusLine()
    {
        if (!GameController.InGame) return "Not in game";

        var counts = MinionKinds.All.Select(kind => $"{kind}: {_tracker.Minions.Count(m => m.Kind == kind)}");
        var status = $"Tracking - {string.Join("   ", counts)}";

        if (Settings.FlameLink.TrackLinkState)
            status += $"   |   missing link: {_tracker.Minions.Count(m => !m.IsLinked)}";

        if (Settings.FlameLink.AutoCast)
        {
            var target = string.IsNullOrEmpty(_linkCaster.LastTargetName) ? "" : $" -> {_linkCaster.LastTargetName}";
            status += $"\nFlame Link: {_linkCaster.LastBlockReason}{target}   |   " +
                      $"gap: {_linkCaster.EffectiveCooldownMs} ms   |   sent: {_linkCaster.CastCount}";
        }

        if (Settings.MercenaryRecall.Enable)
            status += $"\nMercenary recall: {_recallCaster.MercenaryStatus}   |   " +
                      $"gap: {_recallCaster.EffectiveGapMs(Settings.MercenaryRecall)} ms   |   " +
                      $"sent: {_recallCaster.MercenaryRecallCount}";

        if (Settings.MinionRecall.Enable)
            status += $"\nMinion recall: {_recallCaster.MinionStatus}   |   " +
                      $"gap: {_recallCaster.EffectiveGapMs(Settings.MinionRecall)} ms   |   " +
                      $"sent: {_recallCaster.MinionRecallCount}";

        return status;
    }

    private bool ShouldRun() =>
        Settings.Enable && GameController.InGame && GameController.Player is { IsValid: true };

    private void RegisterDiscoveryHotkey() =>
        Input.RegisterKey(Settings.Discovery.ToggleWindowHotkey.Value);

    private void RegisterCastHotkey() =>
        Input.RegisterKey(Settings.FlameLink.CastHotkey.Value);

    private void CopyDiscoveryReport()
    {
        Settings.Discovery.ShowWindow.Value = true;
        _tracker.Refresh(collectCandidates: true);

        var report = string.Join('\n', _tracker.Candidates.Select(c => string.Join('\t',
            c.MatchedKind?.ToString() ?? "-",
            c.MatchSource,
            string.IsNullOrEmpty(c.SourceSkill) ? "-" : c.SourceSkill,
            c.Entity.RenderName ?? "",
            c.Entity.Metadata ?? "",
            c.Buffs)));

        ImGui.SetClipboardText($"MatchedAs\tFoundBy\tSummoningSkill\tName\tMetadata\tBuffs\n{report}");
    }
}
