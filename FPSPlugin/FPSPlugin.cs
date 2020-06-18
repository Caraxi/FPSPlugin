using Dalamud.Game.Internal;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Hooking;

namespace FPSPlugin {
    public class FPSPlugin : IDalamudPlugin {
        public string Name => "FPS Plugin";
        public DalamudPluginInterface PluginInterface { get; private set; }
        public FPSPluginConfig PluginConfig { get; private set; }

        private bool drawConfigWindow = false;
        private bool gameUIHidden = false;
        private bool chatHidden = false;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetBaseUIObjDelegate();

        [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr GetUI2ObjByNameDelegate(IntPtr getBaseUIObj, string uiName, int index = 1);

        private GetBaseUIObjDelegate getBaseUIObj;
        private GetUI2ObjByNameDelegate getUI2ObjByName;

        private delegate IntPtr ToggleUIDelegate(IntPtr baseAddress, byte unknownByte);

        private Hook<ToggleUIDelegate> toggleUIHook;

        private IntPtr chatLogObject;

        private List<float> fpsHistory;
        private Stopwatch fpsHistoryInterval;
        private string fpsText;
        private Vector2 windowSize = Vector2.One;

        public void Dispose() {
            PluginInterface.UiBuilder.OnBuildUi -= this.BuildUI;
            PluginInterface.UiBuilder.OnOpenConfigUi -= this.OnConfigCommandHandler;
            PluginInterface.Framework.OnUpdateEvent -= this.OnFrameworkUpdate;
            fpsHistoryInterval?.Stop();
            getBaseUIObj = null;
            getUI2ObjByName = null;
            toggleUIHook?.Disable();
            RemoveCommands();
            PluginInterface.Dispose();
        }

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = (FPSPluginConfig) pluginInterface.GetPluginConfig() ?? new FPSPluginConfig();
            this.PluginConfig.Init(this, pluginInterface);
            fpsText = string.Empty;
            fpsHistory = new List<float>();
            fpsHistoryInterval = new Stopwatch();
            fpsHistoryInterval.Start();

            SetupCommands();

            var getBaseUIObjScan = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 b8 01 00 00 00 48 8d 15 ?? ?? ?? ?? 48 8b 48 20 e8 ?? ?? ?? ?? 48 8b cf");
            var getUI2ObjByNameScan = PluginInterface.TargetModuleScanner.ScanText("e8 ?? ?? ?? ?? 48 8b cf 48 89 87 ?? ?? 00 00 e8 ?? ?? ?? ?? 41 b8 01 00 00 00");
            this.getBaseUIObj = Marshal.GetDelegateForFunctionPointer<GetBaseUIObjDelegate>(getBaseUIObjScan);
            this.getUI2ObjByName = Marshal.GetDelegateForFunctionPointer<GetUI2ObjByNameDelegate>(getUI2ObjByNameScan);
            this.chatLogObject = this.getUI2ObjByName(Marshal.ReadIntPtr(this.getBaseUIObj(), 32), "ChatLog", 1);


            var toggleUiPtr = pluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 0F B6 B9 ?? ?? ?? ?? B8 ?? ?? ?? ??");
            toggleUIHook = new Hook<ToggleUIDelegate>(toggleUiPtr, new ToggleUIDelegate(((ptr, b) => {
                gameUIHidden = (Marshal.ReadByte(ptr, 104008) & 4) == 0;
                return toggleUIHook.Original(ptr, b);
            })));

            toggleUIHook.Enable();

            PluginInterface.UiBuilder.OnBuildUi += this.BuildUI;
            PluginInterface.UiBuilder.OnOpenConfigUi += OnConfigCommandHandler;
            PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;
        }

        private void OnFrameworkUpdate(Framework framework) {
            try {
                if (fpsHistoryInterval.ElapsedMilliseconds > 1000) {
                    fpsHistoryInterval.Restart();
                    // FPS values are only updated in memory once per second.
                    var fps = Marshal.PtrToStructure<float>(PluginInterface.Framework.Address.BaseAddress + 0x165C);
                    fpsText = PluginConfig.ShowDecimals ? $"FPS: {fps:F2}" : $"FPS: {fps:F0}";
                    if (PluginConfig.ShowAverage || PluginConfig.ShowMinimum) {
                        fpsHistory.Add(fps);
                        if (fpsHistory.Count > PluginConfig.HistorySnapshotCount) {
                            fpsHistory.RemoveRange(0, fpsHistory.Count - PluginConfig.HistorySnapshotCount);
                        }

                        if (PluginConfig.ShowAverage) {
                            fpsText += PluginConfig.ShowDecimals ? $" / Average: {fpsHistory.Average():F2}" : $" / Average: {fpsHistory.Average():F0}";
                        }

                        if (PluginConfig.ShowMinimum) {
                            fpsText += PluginConfig.ShowDecimals ? $" / Min: {fpsHistory.Min():F2}" : $" / Min: {fpsHistory.Min():F0}";
                        }
                    }

                    windowSize = Vector2.Zero;
                }


                // https://github.com/karashiiro/PingPlugin
                if (this.PluginInterface.ClientState.LocalPlayer == null) {
                    chatHidden = false;
                    this.chatLogObject = IntPtr.Zero;
                    return;
                }

                if (chatLogObject == IntPtr.Zero) {
                    var baseUi = this.getBaseUIObj();
                    if (baseUi == IntPtr.Zero) return;
                    var baseOffset = Marshal.ReadIntPtr(baseUi, 32);
                    if (baseOffset == IntPtr.Zero) return;
                    this.chatLogObject = this.getUI2ObjByName(baseOffset, "ChatLog", 1);
                    return;
                }

                var chatLogProperties = Marshal.ReadIntPtr(this.chatLogObject, 0xC8);
                if (chatLogProperties == IntPtr.Zero) {
                    this.chatHidden = true;
                    return;
                }

                chatHidden = Marshal.ReadByte(chatLogProperties + 0x73) == 0;
            } catch (Exception ex) {
                PluginLog.LogError(ex.Message);
                chatHidden = false;
                this.chatLogObject = IntPtr.Zero;
            }
        }

        public void SetupCommands() {
            PluginInterface.CommandManager.AddHandler("/pfps", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {this.Name}. /pfps [show|hide|toggle|reset]",
                ShowInHelp = true
            });
        }


        public void OnConfigCommandHandler(object a, object b) {
            if (b is string s) {
                switch (s.ToLower()) {
                    case "toggle": {
                        PluginConfig.Enable = !PluginConfig.Enable;
                        break;
                    }
                    case "show": {
                        PluginConfig.Enable = true;
                        break;
                    }
                    case "hide": {
                        PluginConfig.Enable = false;
                        break;
                    }
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
            PluginInterface.CommandManager.RemoveHandler("/pfps");
        }

        private void BuildUI() {
            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();
            if ((gameUIHidden || chatHidden) && PluginConfig.HideInCutscene || !PluginConfig.Enable || string.IsNullOrEmpty(fpsText)) return;
            ImGui.SetNextWindowBgAlpha(PluginConfig.Alpha);

            var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse;

            if (PluginConfig.Locked) {
                flags |= ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoMove;
            }

            if (windowSize == Vector2.Zero) {
                windowSize = ImGui.CalcTextSize(fpsText) + (ImGui.GetStyle().WindowPadding * 2);
            }

            ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
            ImGui.Begin("FPS##fpsPluginMonitorWindow", flags);
            ImGui.TextColored(PluginConfig.Colour, fpsText);
            ImGui.End();
        }
    }
}
