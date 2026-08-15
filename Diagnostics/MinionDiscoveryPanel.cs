using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using ImGuiNET;
using LuminaryHelper.Casting;
using LuminaryHelper.Minions;
using LuminaryHelper.Settings;

namespace LuminaryHelper.Diagnostics;

public sealed class MinionDiscoveryPanel(LuminaryHelperSettings settings)
{
    private static readonly Vector4 ErrorColor = new(1f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 ReadyColor = new(0.4f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 MutedColor = new(0.6f, 0.6f, 0.6f, 1f);

    public void Draw(IReadOnlyList<DiscoveredCandidate> candidates, IReadOnlyList<SkillRow> skills, string lastError)
    {
        if (!settings.Discovery.ShowWindow) return;

        var isOpen = true;
        ImGui.SetNextWindowSize(new Vector2(900, 500), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("LuminaryHelper - discovery", ref isOpen))
        {
            if (!string.IsNullOrEmpty(lastError))
                ImGui.TextColored(ErrorColor, $"Last scan error: {lastError}");

            var rows = settings.Discovery.OnlyUnmatched
                ? candidates.Where(c => c.MatchedKind == null).ToList()
                : candidates.ToList();

            ImGui.Text($"{rows.Count} entit{(rows.Count == 1 ? "y" : "ies")} considered");
            ImGui.SameLine();
            if (ImGui.Button("Copy to clipboard")) ImGui.SetClipboardText(BuildReport(rows));

            ImGui.Separator();
            DrawEntities(rows);

            ImGui.Separator();
            DrawSkills(skills);
        }

        ImGui.End();

        if (!isOpen) settings.Discovery.ShowWindow.Value = false;
    }

    private static void DrawEntities(List<DiscoveredCandidate> rows)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
                                      ImGuiTableFlags.ScrollX;

        if (!ImGui.BeginTable("candidates", 6, flags, new Vector2(0, 220))) return;

        ImGui.TableSetupColumn("Matched as", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Found by", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Summoning skill", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 160);
        ImGui.TableSetupColumn("Metadata", ImGuiTableColumnFlags.WidthFixed, 320);
        ImGui.TableSetupColumn("Buffs (* = from you)", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        foreach (var row in rows)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            if (row.MatchedKind is { } kind)
                ImGui.TextUnformatted(kind.ToString());
            else
                ImGui.TextColored(MutedColor, "-");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.MatchSource.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(string.IsNullOrEmpty(row.SourceSkill) ? "-" : row.SourceSkill);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Entity.RenderName ?? "");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Entity.Metadata ?? "");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Buffs);
        }

        ImGui.EndTable();
    }

    private static void DrawSkills(IReadOnlyList<SkillRow> skills)
    {
        if (!ImGui.CollapsingHeader($"My skills ({skills.Count})", ImGuiTreeNodeFlags.DefaultOpen)) return;

        if (skills.Count == 0)
        {
            ImGui.TextDisabled("No skills read yet.");
            return;
        }

        ImGui.TextDisabled("Internal names go in the Flame Link and Recall skill name boxes.");

        if (ImGui.Button("Copy skill list"))
            ImGui.SetClipboardText("InternalName\tName\tMaxCooldown\tUses\tCastTimeMs\tReady\n" +
                string.Join('\n', skills.Select(s => string.Join('\t',
                    s.InternalName, s.DisplayName, s.MaxCooldown, $"{s.RemainingUses}/{s.TotalUses}",
                    s.CastTimeMs, s.Ready))));

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
                                      ImGuiTableFlags.ScrollX;

        if (!ImGui.BeginTable("skills", 6, flags, new Vector2(0, 220))) return;

        ImGui.TableSetupColumn("Internal name", ImGuiTableColumnFlags.WidthFixed, 220);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 180);
        ImGui.TableSetupColumn("Cooldown", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn("Uses left", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn("Cast time", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn("Ready now", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        foreach (var skill in skills)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(skill.InternalName);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(skill.DisplayName);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(skill.MaxCooldown > 0 ? $"{skill.MaxCooldown:0.##}s" : "-");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(skill.TotalUses > 0 ? $"{skill.RemainingUses}/{skill.TotalUses}" : "-");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(skill.CastTimeMs > 0 ? $"{skill.CastTimeMs} ms" : "-");

            ImGui.TableNextColumn();
            if (skill.Ready)
                ImGui.TextColored(ReadyColor, "yes");
            else
                ImGui.TextDisabled("no");
        }

        ImGui.EndTable();
    }

    private static string BuildReport(List<DiscoveredCandidate> rows)
    {
        var report = new StringBuilder();
        report.AppendLine("MatchedAs\tFoundBy\tSummoningSkill\tName\tMetadata\tBuffs");

        foreach (var row in rows)
            report.AppendLine(string.Join('\t',
                row.MatchedKind?.ToString() ?? "-",
                row.MatchSource,
                string.IsNullOrEmpty(row.SourceSkill) ? "-" : row.SourceSkill,
                row.Entity.RenderName ?? "",
                row.Entity.Metadata ?? "",
                row.Buffs));

        return report.ToString();
    }
}
