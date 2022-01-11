using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Logging;

namespace FPSPlugin {
    public class FPSPlugin : IDalamudPlugin {
        public string Name => "FPS Plugin";
        public FPSPluginConfig PluginConfig { get; private set; }

        private bool drawConfigWindow;
        
        private List<float> fpsHistory;
        private Stopwatch fpsHistoryInterval;
        private string fpsText;
        private Vector2 windowSize = Vector2.One;

        private bool fontBuilt;
        private bool fontLoadFailed;
        private ImFontPtr font;
        private float maxSeenFps;

        [PluginService] public static  CommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static  Framework Framework { get; private set; } = null!;
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;

        public void Dispose() {
            PluginInterface.UiBuilder.Draw -= this.BuildUI;
            PluginInterface.UiBuilder.BuildFonts -= this.BuildFont;
            PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
            Framework.Update -= this.OnFrameworkUpdate;
            PluginInterface.UiBuilder.RebuildFonts();
            fpsHistoryInterval?.Stop();
            RemoveCommands();
            PluginInterface.Dispose();
        }

        public FPSPlugin() {
            this.PluginConfig = (FPSPluginConfig) PluginInterface.GetPluginConfig() ?? new FPSPluginConfig();
            this.PluginConfig.Init(this);
            fpsText = string.Empty;
            fpsHistory = new List<float>();

            fpsHistoryInterval = new Stopwatch();
            fpsHistoryInterval.Start();
            SetupCommands();
            PluginInterface.UiBuilder.Draw += this.BuildUI;
            PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
            Framework.Update += OnFrameworkUpdate;
            PluginInterface.UiBuilder.BuildFonts += this.BuildFont;
        }

        private string FormatFpsValue(float value) {
            if (maxSeenFps > 1000) return PluginConfig.ShowDecimals ? $"{value,8:####0.00}" : $"{value,5:####0}";
            if (maxSeenFps > 100) return PluginConfig.ShowDecimals ? $"{value,7:###0.00}" : $"{value,4:###0}";
            return PluginConfig.ShowDecimals ? $"{value,6:##0.00}" : $"{value,3:##0}";
        }

        private void OnFrameworkUpdate(Framework framework) {
            try {
                if (!(fontBuilt || fontLoadFailed)) return;
                if (fpsHistoryInterval.ElapsedMilliseconds > 1000) {
                    fpsHistoryInterval.Restart();
                    // FPS values are only updated in memory once per second.
                    var fps = Marshal.PtrToStructure<float>(Framework.Address.BaseAddress + 0x17C4);
                    var windowInactive = Marshal.ReadByte(framework.Address.BaseAddress, 0x17D0) == 1;
                    if (fps > maxSeenFps) maxSeenFps = fps;

                    fpsText = (PluginConfig.ShowLabel ? "FPS:" : "") + FormatFpsValue(fps);
                    if (PluginConfig.ShowAverage || PluginConfig.ShowMinimum) {
                        if (!windowInactive) fpsHistory.Add(fps);

                        if (fpsHistory.Count > PluginConfig.HistorySnapshotCount) {
                            fpsHistory.RemoveRange(0, fpsHistory.Count - PluginConfig.HistorySnapshotCount);
                        }

                        if (PluginConfig.ShowAverage && fpsHistory.Count > 0) {
                            fpsText += PluginConfig.MultiLine ? "\n" : " / ";
                            fpsText += $"Avg:{FormatFpsValue(fpsHistory.Average())}";
                        }

                        if (PluginConfig.ShowMinimum && fpsHistory.Count > 0) {
                            fpsText += PluginConfig.MultiLine ? "\n" : " / ";
                            fpsText += $"Min:{FormatFpsValue(fpsHistory.Min())}";
                        }
                    }
#if DEBUG
                    if (!string.IsNullOrEmpty(PluginConfig.TestText)) {
                        fpsText = PluginConfig.TestText;
                    }
#endif
                    windowSize = Vector2.Zero;
                }

            } catch (Exception ex) {
                PluginLog.LogError(ex.Message);
            }
        }

        public void SetupCommands() {
            CommandManager.AddHandler("/pfps", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {this.Name}. /pfps [show|hide|toggle|reset]",
                ShowInHelp = true
            });
        }

        private void OpenConfigUi() {
            OnConfigCommandHandler(null, null);
        }

        public void OnConfigCommandHandler(string command, string args) {
            if (args != null) {
                switch (args.ToLower()) {
                    case "t":
                    case "toggle": {
                        PluginConfig.Enable = !PluginConfig.Enable;
                        break;
                    }
                    case "s":
                    case "show": {
                        PluginConfig.Enable = true;
                        break;
                    }
                    case "h":
                    case "hide": {
                        PluginConfig.Enable = false;
                        break;
                    }
                    case "r":
                    case "reset": {
                        fpsHistory.Clear();
                        break;
                    }
                    default: {
                        drawConfigWindow = true;
                        break;
                    }
                }

                PluginConfig.Save();
            } else {
                drawConfigWindow = true;
            }
        }

        public void RemoveCommands() {
            CommandManager.RemoveHandler("/pfps");
        }

        private string GetFontPath(FPSPluginFont font) {
            return font switch {
                FPSPluginFont.DalamudDefault => Path.Combine(PluginInterface.DalamudAssetDirectory.FullName, "UIRes", "NotoSansCJKjp-Medium.otf"),
                _ => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "font.ttf"),
            };
        }
        
        private void BuildFont() {
            var fontFile = GetFontPath(PluginConfig.Font);
            fontBuilt = false;
            if (File.Exists(fontFile)) {
                try {
                    font = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, PluginConfig.FontSize);
                    fontBuilt = true;
                } catch (Exception ex) {
                    PluginLog.Log($"Font failed to load. {fontFile}");
                    PluginLog.Log(ex.ToString());
                    fontLoadFailed = true;
                }
            } else {
                PluginLog.Log($"Font doesn't exist. {fontFile}");
                fontLoadFailed = true;
            }
        }

        internal void ReloadFont() {
            PluginInterface.UiBuilder.RebuildFonts();
        }
        
        private void BuildUI() {

            if (!fontBuilt && !fontLoadFailed) {
                PluginInterface.UiBuilder.RebuildFonts();
                return;
            }

            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();

            if (PluginConfig.FontChangeTime > 0) {
                if (DateTime.Now.Ticks - 10000000 > PluginConfig.FontChangeTime) {
                    PluginConfig.FontChangeTime = 0;
                    fontLoadFailed = false;
                    windowSize = Vector2.Zero;
                    ReloadFont();
                }
            }

            if (!PluginConfig.Enable || string.IsNullOrEmpty(fpsText)) return;

            ImGui.SetNextWindowBgAlpha(PluginConfig.Alpha);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, PluginConfig.WindowPadding);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, PluginConfig.WindowCornerRounding);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoFocusOnAppearing;

            if (PluginConfig.Locked) {
                flags |= ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus;
            }

            if (fontBuilt) ImGui.PushFont(font);
            if (windowSize == Vector2.Zero) {
                windowSize = ImGui.CalcTextSize(fpsText) + (ImGui.GetStyle().WindowPadding * 2);
            }
            
            ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
            ImGui.Begin("FPS##fpsPluginMonitorWindow", flags);
            ImGui.TextColored(PluginConfig.Colour, fpsText);
            ImGui.End();
            ImGui.PopStyleVar(6);
            if (fontBuilt) ImGui.PopFont();
        }
    }
}
