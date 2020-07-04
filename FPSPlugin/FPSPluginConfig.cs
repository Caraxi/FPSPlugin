using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Numerics;

namespace FPSPlugin {
    public class FPSPluginConfig : IPluginConfiguration {
        [NonSerialized] private DalamudPluginInterface pluginInterface;

        [NonSerialized] private FPSPlugin plugin;

        [NonSerialized] public long FontChangeTime;

        public int Version { get; set; }

        public bool Locked { get; set; }

        public float Alpha { get; set; }

        public bool ShowDecimals { get; set; }

        public Vector4 Colour { get; set; }

        public bool HideInCutscene { get; set; }

        public bool Enable { get; set; }

        public int HistorySnapshotCount { get; set; }

        public bool ShowAverage { get; set; }
        public bool ShowMinimum { get; set; }
        public float FontSize { get; set; } = 16;

        public bool MultiLine { get; set; }

        public FPSPluginConfig() {
            LoadDefaults();
        }

        public void LoadDefaults() {
            Colour = new Vector4(0, 1, 1, 1);
            Alpha = 0.5f;
            Locked = false;
            HideInCutscene = true;
            ShowDecimals = false;
            HistorySnapshotCount = 300;
            ShowAverage = false;
            MultiLine = false;
            FontSize = 16;
            FontChangeTime = DateTime.Now.Ticks;
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

            var enabled = Enable;
            if (ImGui.Checkbox("Show Display##fpsPluginEnabledSetting", ref enabled)) {
                Enable = enabled;
                Save();
            }

            ImGui.SameLine();

            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "/pfps [show|hide|toggle]");

            var locked = Locked;
            if (ImGui.Checkbox("Lock Display##fpsPluginLockSetting", ref locked)) {
                Locked = locked;
                Save();
            }

            var hideInCutscene = HideInCutscene;
            if (ImGui.Checkbox("Hide when chat is hidden##fpsPluginHideSetting", ref hideInCutscene)) {
                HideInCutscene = hideInCutscene;
                Save();
            }

            var decimals = ShowDecimals;
            if (ImGui.Checkbox("Show Decimals##fpsPluginDecimalsSetting", ref decimals)) {
                ShowDecimals = decimals;
                Save();
            }

            var showAverage = ShowAverage;
            if (ImGui.Checkbox("Show Average##fpsPluginShowAverageSetting", ref showAverage)) {
                ShowAverage = showAverage;
                Save();
            }

            var showMin = ShowMinimum;
            if (ImGui.Checkbox("Show Minimum##fpsPluginShowMinimumSetting", ref showMin)) {
                ShowMinimum = showMin;
                Save();
            }

            var multiLine = MultiLine;
            if (ImGui.Checkbox("Multiline##fpsPluginMultiline", ref multiLine)) {
                MultiLine = multiLine;
                Save();
            }

            var bgAlpha = Alpha;
            if (ImGui.SliderFloat("Background Opacity##fpsPluginOpacitySetting", ref bgAlpha, 0, 1)) {
                Alpha = Math.Max(0, Math.Min(1, bgAlpha));
                Save();
            }

            var historySnapshotCount = HistorySnapshotCount;
            if (ImGui.InputInt("Tracking Timespan (Seconds)", ref historySnapshotCount, 1, 60)) {
                if (historySnapshotCount < 1) {
                    historySnapshotCount = 1;
                }

                if (historySnapshotCount > 10000) {
                    historySnapshotCount = 10000;
                }

                HistorySnapshotCount = historySnapshotCount;
                Save();
            }

            var fontSize = (int) FontSize;
            if (ImGui.SliderInt("Font Size##fpsPluginFontSizeSetting", ref fontSize, 6, 60)) {
                FontSize = fontSize;
                FontChangeTime = DateTime.Now.Ticks;
                Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Reload Font")) {
                plugin.ReloadFont();
            }

            var colour = Colour;
            if (ImGui.ColorEdit4("Text Colour##fpsPluginColorSetting", ref colour)) {
                Colour = colour;
                Save();
            }

            ImGui.Separator();
            if (ImGui.Button("Restore Default##fpsPluginDefaultsButton")) {
                LoadDefaults();
                Save();
            }

            ImGui.End();

            return drawConfig;
        }
    }
}
