using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Numerics;
using System.Reflection;

namespace FPSPlugin {
    public class FPSPluginConfig : IPluginConfiguration {
        [NonSerialized] private DalamudPluginInterface pluginInterface;
        [NonSerialized] private FPSPlugin plugin;
        [NonSerialized] public long FontChangeTime = DateTime.Now.Ticks;
        [NonSerialized] public string TestText = string.Empty;

        public int Version { get; set; }

        public bool Locked;
        public bool ShowDecimals;
        public bool Enable = true;
        public bool ShowAverage;
        public bool ShowMinimum;
        public bool MultiLine;

        public float Alpha = 0.5f;
        public float FontSize = 16;
        public float WindowCornerRounding;
        
        public int HistorySnapshotCount = 300;

        public Vector4 Colour = new Vector4(0, 1, 1, 1);
        public Vector2 WindowPadding = new Vector2(4, 4);

        public void LoadDefaults() {
            var defaults = new FPSPluginConfig();
            foreach (var f in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                f.SetValue(this, f.GetValue(defaults));
            }
        }

        public void Init(FPSPlugin plugin, DalamudPluginInterface pluginInterface) {
            this.plugin = plugin;
            this.pluginInterface = pluginInterface;
        }

        public void Save() {
            pluginInterface.SavePluginConfig(this);
        }

        public bool DrawConfigUI() {
            var drawConfig = true;
            var windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
            ImGui.Begin($"{plugin.Name} Config##fpsPluginConfigWindow", ref drawConfig, windowFlags);

            var changed = false;

            changed |= ImGui.Checkbox("Show Display##fpsPluginEnabledSetting", ref Enable);
            ImGui.SameLine();
            ImGui.TextDisabled("/pfps [show|hide|toggle]");

            changed |= ImGui.Checkbox("Lock Display##fpsPluginLockSetting", ref Locked);
            changed |= ImGui.Checkbox("Show Decimals##fpsPluginDecimalsSetting", ref ShowDecimals);
            changed |= ImGui.Checkbox("Show Average##fpsPluginShowAverageSetting", ref ShowAverage);
            changed |= ImGui.Checkbox("Show Minimum##fpsPluginShowMinimumSetting", ref ShowMinimum);
            changed |= ImGui.Checkbox("Multiline##fpsPluginMultiline", ref MultiLine);
            changed |= ImGui.InputInt("Tracking Timespan (Seconds)", ref HistorySnapshotCount, 1, 60);

            if (ImGui.TreeNode("Style Options###fpsPluginStyleOptions")) {
                changed |= ImGui.SliderFloat("Background Opacity##fpsPluginOpacitySetting", ref Alpha, 0, 1);
                if (ImGui.SliderFloat("Font Size##fpsPluginFontSizeSetting", ref FontSize, 6, 90, "%.0f")) {
                    FontChangeTime = DateTime.Now.Ticks;
                    changed = true;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Reload Font")) {
                    plugin.ReloadFont();
                }
                changed |= ImGui.ColorEdit4("Text Colour##fpsPluginColorSetting", ref Colour);

                changed |= ImGui.SliderFloat("Corner Rounding###fpsPluginCornerRounding", ref WindowCornerRounding, 0f, 20f, "%.0f");
                changed |= ImGui.SliderFloat2("Window Padding###fpsPluginWindowPadding", ref WindowPadding, 0f, 20f, "%.0f");

                ImGui.TreePop();
            }
            
#if DEBUG
            ImGui.InputText("Test Text", ref TestText, 100, ImGuiInputTextFlags.Multiline);
#endif

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

            ImGui.End();

            return drawConfig;
        }
    }
}
