using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using ImGuiNET;
using LinkHelper.Casting;
using LinkHelper.Players;
using LinkHelper.Settings;

namespace LinkHelper.Diagnostics;

public sealed class PlayerDiscoveryPanel(LinkHelperSettings settings)
{
    private static readonly Vector4 ErrorColor = new(1f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 ReadyColor = new(0.4f, 0.9f, 0.4f, 1f);
    private static readonly Vector4 MutedColor = new(0.6f, 0.6f, 0.6f, 1f);

    public void Draw(
        IReadOnlyList<DiscoveredPlayer> candidates,
        IReadOnlyList<SkillRow> skills,
        string lastError,
        bool hasActiveSourceBuff)
    {
        if (!settings.Discovery.ShowWindow) return;

        var isOpen = true;
        ImGui.SetNextWindowSize(new Vector2(900, 500), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("LinkHelper - discovery", ref isOpen))
        {
            if (!string.IsNullOrEmpty(lastError))
                ImGui.TextColored(ErrorColor, $"Last scan error: {lastError}");

            if (hasActiveSourceBuff)
                ImGui.TextColored(ReadyColor, "Your source buff is active");
            else
                ImGui.TextColored(MutedColor, "Your source buff is not active");

            ImGui.Text($"{candidates.Count} player{(candidates.Count == 1 ? "" : "s")} nearby");
            ImGui.SameLine();
            if (ImGui.Button("Copy to clipboard")) ImGui.SetClipboardText(BuildReport(candidates));

            ImGui.Separator();
            DrawPlayers(candidates);

            ImGui.Separator();
            DrawSkills(skills);
        }

        ImGui.End();

        if (!isOpen) settings.Discovery.ShowWindow.Value = false;
    }

    private static void DrawPlayers(IReadOnlyList<DiscoveredPlayer> rows)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY |
                                      ImGuiTableFlags.ScrollX;

        if (!ImGui.BeginTable("players", 4, flags, new Vector2(0, 220))) return;

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("Distance", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn("Linked", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Buffs (* = from you)", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        foreach (var row in rows)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(PlayerIdentity.KeyFor(row.Entity));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{row.Entity.DistancePlayer:0}");

            ImGui.TableNextColumn();
            if (row.IsLinked)
                ImGui.TextColored(ReadyColor, "yes");
            else
                ImGui.TextColored(MutedColor, "no");

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

        ImGui.TextDisabled("Internal names go in the \"Link internal name\" box under Link.");

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

    private static string BuildReport(IReadOnlyList<DiscoveredPlayer> rows)
    {
        var report = new StringBuilder();
        report.AppendLine("Name\tDistance\tLinked\tBuffs");

        foreach (var row in rows)
            report.AppendLine(string.Join('\t',
                PlayerIdentity.KeyFor(row.Entity),
                $"{row.Entity.DistancePlayer:0}",
                row.IsLinked ? "yes" : "no",
                row.Buffs));

        return report.ToString();
    }
}
