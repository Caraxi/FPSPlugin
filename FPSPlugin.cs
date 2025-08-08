using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Gui.Dtr;
using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace FPSPlugin {
    public class FPSPlugin : IDalamudPlugin {
        public string Name => "FPS Plugin";
        public FPSPluginConfig PluginConfig { get; }

        private bool drawConfigWindow;
        
        private List<float> fpsHistory;
        private Stopwatch fpsHistoryInterval;
        private string fpsText;
        private IDtrBarEntry dtrEntry;
        private float maxSeenFps;

        [PluginService] public static  ICommandManager CommandManager { get; set; } = null!;
        [PluginService] public static  IFramework Framework { get; set; } = null!;
        [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] public static IDtrBar DtrBar { get; set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; set; } = null!;

        public void Dispose() {
            PluginInterface.UiBuilder.Draw -= this.BuildUI;
            PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
            Framework.Update -= this.OnFrameworkUpdate;
            fpsHistoryInterval?.Stop();
            dtrEntry?.Remove();
            RemoveCommands();
        }

        public FPSPlugin() {
            PluginConfig = (FPSPluginConfig) PluginInterface.GetPluginConfig() ?? new FPSPluginConfig();
            PluginConfig.Init(this);
            fpsText = string.Empty;
            fpsHistory = new List<float>();

            fpsHistoryInterval = new Stopwatch();
            fpsHistoryInterval.Start();
            SetupCommands();
            PluginInterface.UiBuilder.Draw += this.BuildUI;
            PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
            Framework.Update += OnFrameworkUpdate;
        }

        private string FormatFpsValue(float value) {
            if (maxSeenFps > 1000) return PluginConfig.ShowDecimals ? $"{value,8:####0.00}" : $"{value,5:####0}";
            if (maxSeenFps > 100) return PluginConfig.ShowDecimals ? $"{value,7:###0.00}" : $"{value,4:###0}";
            return PluginConfig.ShowDecimals ? $"{value,6:##0.00}" : $"{value,3:##0}";
        }

        private unsafe void OnFrameworkUpdate(IFramework dFramework) {
            var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
            try {
                if (fpsText != null) {
                    dtrEntry ??= DtrBar.Get("FPS Display");
                    dtrEntry.Shown = PluginConfig.Enable;
                    dtrEntry.Text = fpsText;
                    dtrEntry.OnClick = PluginConfig.DtrOpenSettings ? OpenConfigUi : null;
                    dtrEntry.Tooltip = PluginConfig.DtrTooltip && fpsHistory.Count > 0 ? $"Average:{FormatFpsValue(fpsHistory.Average())}\nMinimum:{FormatFpsValue(fpsHistory.Min())}" : null;
                } else {
                    if (dtrEntry != null) dtrEntry.Shown = false;
                }
                
                if (fpsHistoryInterval.ElapsedMilliseconds > 1000) {
                    fpsHistoryInterval.Restart();
                    // FPS values are only updated in memory once per second.
                    var fps = framework->FrameRate;
                    var windowInactive = framework->WindowInactive;
                    if (fps > maxSeenFps) maxSeenFps = fps;

                    fpsText = string.Empty;
                    if (!PluginConfig.NoLabels && !PluginConfig.AlternativeFPSLabel) fpsText += "FPS:";
                    fpsText += $"{FormatFpsValue(fps)}";
                    if (!PluginConfig.NoLabels && PluginConfig.AlternativeFPSLabel) fpsText += "fps";
                    if (PluginConfig.ShowAverage || PluginConfig.ShowMinimum || PluginConfig.DtrTooltip) {
                        if (!windowInactive) fpsHistory.Add(fps);

                        if (fpsHistory.Count > PluginConfig.HistorySnapshotCount) {
                            fpsHistory.RemoveRange(0, fpsHistory.Count - PluginConfig.HistorySnapshotCount);
                        }

                        if (PluginConfig.ShowAverage && fpsHistory.Count > 0) {
                            fpsText +=  " / ";
                            if (!PluginConfig.NoLabels) fpsText += "Avg:";
                            fpsText += $"{FormatFpsValue(fpsHistory.Average())}";
                        }

                        if (PluginConfig.ShowMinimum && fpsHistory.Count > 0) {
                            fpsText += " / ";
                            if (!PluginConfig.NoLabels) fpsText += "Min:";
                            fpsText += $"{FormatFpsValue(fpsHistory.Min())}";
                        }
                    }
#if DEBUG
                    if (!string.IsNullOrEmpty(PluginConfig.TestText)) {
                        fpsText = PluginConfig.TestText;
                    }
#endif
                }

            } catch (Exception ex) {
                PluginLog.Error(ex.Message);
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
        
        private void OpenConfigUi(DtrInteractionEvent obj) {
            OpenConfigUi();
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
        
        private void BuildUI() {
            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();
        }
    }
}
