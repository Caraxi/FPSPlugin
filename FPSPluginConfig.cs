using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using System;
using System.Diagnostics;
using System.Reflection;

namespace FPSPlugin {
    
    public class FPSPluginConfig : IPluginConfiguration {
        [NonSerialized] private FPSPlugin plugin = null!;
        [NonSerialized] public string TestText = string.Empty;

        public int Version { get; set; }

        public bool ShowDecimals;
        public bool Enable = true;
        public bool ShowAverage;
        public bool ShowMinimum;
        public bool NoLabels;
        public bool AlternativeFPSLabel;
        public bool DtrTooltip = true;
        public bool DtrOpenSettings = true;

        public int HistorySnapshotCount = 300;

        public void LoadDefaults() {
            var defaults = new FPSPluginConfig();
            foreach (var f in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                f.SetValue(this, f.GetValue(defaults));
            }
        }

        public void Init(FPSPlugin plugin) {
            this.plugin = plugin;
        }

        public void Save() {
            FPSPlugin.PluginInterface.SavePluginConfig(this);
        }

        public bool DrawConfigUI() {
            var drawConfig = true;
            var windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
            ImGui.Begin($"{plugin.Name} Config##fpsPluginConfigWindow", ref drawConfig, windowFlags);

            var changed = false;

            changed |= ImGui.Checkbox("Show Display##fpsPluginEnabledSetting", ref Enable);
            ImGui.SameLine();
            ImGui.TextDisabled("/pfps [show|hide|toggle]");




            changed |= ImGui.Checkbox("Enable Tooltip##fpsPluginDtrTooltip", ref DtrTooltip);
            changed |= ImGui.Checkbox("Click to open settings##fpsPluginDtrOpenSettings", ref DtrOpenSettings);
                
            changed |= ImGui.Checkbox("Show Decimals##fpsPluginDecimalsSetting", ref ShowDecimals);
            changed |= ImGui.Checkbox("Show Average##fpsPluginShowAverageSetting", ref ShowAverage);
            changed |= ImGui.Checkbox("Show Minimum##fpsPluginShowMinimumSetting", ref ShowMinimum);
            changed |= ImGui.Checkbox("Hide Labels##fpsPluginNoLabelsSetting", ref NoLabels);
            if (!NoLabels) changed |= ImGui.Checkbox("Alternative FPS Label##fpsPluginAlternativeFPSLabelSetting", ref AlternativeFPSLabel);
            changed |= ImGui.InputInt("Tracking Timespan (Seconds)", ref HistorySnapshotCount, 1, 60);

            ImGui.Separator();
            if (ImGui.Button("Restore Default##fpsPluginDefaultsButton")) {
                LoadDefaults();
                changed = true;
            }

            if (changed) {
                if (HistorySnapshotCount < 1) HistorySnapshotCount = 1;
                if (HistorySnapshotCount > 10000) HistorySnapshotCount = 10000;
                Save();
            }
            
            ImGui.SameLine();
            
            ImGui.PushStyleColor(ImGuiCol.Button, 0xFF5E5BFF);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF5E5BAA);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF5E5BDD);
            var c = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.CalcTextSize("Support on Ko-fi").X - ImGui.GetStyle().FramePadding.X * 2);
            if (ImGui.Button("Support on Ko-fi")) {
                Process.Start(new ProcessStartInfo {FileName = "https://ko-fi.com/Caraxi", UseShellExecute = true});
            }
            ImGui.SetCursorPos(c);
            ImGui.PopStyleColor(3);
            
            
            ImGui.End();

            return drawConfig;
        }
    }
}
