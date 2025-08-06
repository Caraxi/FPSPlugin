using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;

namespace FPSPlugin {

    public enum FPSPluginFont {
        [Description("Dalamud Default")]
        DalamudDefault,
        
        [Description("Default (font.ttf)")]
        PluginDefault,
    }

    public static class EnumExtensions {
        public static string Description(this FPSPluginFont value)
        {
            var fi = value.GetType().GetField(value.ToString());
            if (fi is null) return $"{value}";
            var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }
    }
    
    
    public class FPSPluginConfig : IPluginConfiguration {
        [NonSerialized] private FPSPlugin plugin;
        [NonSerialized] public long FontChangeTime = DateTime.Now.Ticks;
        [NonSerialized] public string TestText = string.Empty;

        public int Version { get; set; }

        public bool Locked;
        public bool ShowDecimals;
        public bool Enable = true;
        public bool ShowAverage;
        public bool ShowMinimum;
        public bool NoLabels;
        public bool AlternativeFPSLabel;
        public bool DtrTooltip = true;
        public bool DtrOpenSettings = true;

        public FPSPluginFont Font = FPSPluginFont.PluginDefault;
        
        public float Alpha = 0.5f;
        public float FontSize = 16;
        public float WindowCornerRounding;
        
        public int HistorySnapshotCount = 300;
        public int BorderSize = -1;

        public Vector4 Colour = new Vector4(0, 1, 1, 1);
        public Vector2 WindowPadding = new Vector2(4, 4);

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
