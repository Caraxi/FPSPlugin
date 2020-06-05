using Dalamud.Game.Internal;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace FPSPlugin {
	public class FPSPlugin : IDalamudPlugin {
		public string Name => "FPS Plugin";
		public DalamudPluginInterface PluginInterface { get; private set; }
		public FPSPluginConfig PluginConfig { get; private set; }

		private bool drawConfigWindow = false;
		private bool gameUIHidden = false;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr GetBaseUIObjDelegate();

		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
		private delegate IntPtr GetUI2ObjByNameDelegate(IntPtr getBaseUIObj, string uiName, int index = 1);

		private GetBaseUIObjDelegate getBaseUIObj;
		private GetUI2ObjByNameDelegate getUI2ObjByName;

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
			RemoveCommands();
		}

		public void Initialize(DalamudPluginInterface pluginInterface) {
			this.PluginInterface = pluginInterface;
			this.PluginConfig = (FPSPluginConfig)pluginInterface.GetPluginConfig() ?? new FPSPluginConfig();
			this.PluginConfig.Init(this, pluginInterface);
			fpsText = string.Empty;
			fpsHistory = new List<float>();
			fpsHistoryInterval = new Stopwatch();
			fpsHistoryInterval.Start();

			SetupCommands();

			PluginInterface.UiBuilder.OnBuildUi += this.BuildUI;
			PluginInterface.UiBuilder.OnOpenConfigUi += OnConfigCommandHandler;
			PluginInterface.Framework.OnUpdateEvent += OnFrameworkUpdate;

			IntPtr getBaseUIObjScan = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 b8 01 00 00 00 48 8d 15 ?? ?? ?? ?? 48 8b 48 20 e8 ?? ?? ?? ?? 48 8b cf");
			IntPtr getUI2ObjByNameScan = PluginInterface.TargetModuleScanner.ScanText("e8 ?? ?? ?? ?? 48 8b cf 48 89 87 ?? ?? 00 00 e8 ?? ?? ?? ?? 41 b8 01 00 00 00");
			this.getBaseUIObj = Marshal.GetDelegateForFunctionPointer<GetBaseUIObjDelegate>(getBaseUIObjScan);
			this.getUI2ObjByName = Marshal.GetDelegateForFunctionPointer<GetUI2ObjByNameDelegate>(getUI2ObjByNameScan);
			this.chatLogObject = this.getUI2ObjByName(Marshal.ReadIntPtr(this.getBaseUIObj(), 32), "ChatLog", 1);
		}

		private void OnFrameworkUpdate(Framework framework) {
			try {
				if (fpsHistoryInterval.ElapsedMilliseconds > 1000) {
					fpsHistoryInterval.Restart();
					// FPS values are only updated in memory once per second.
					float fps = Marshal.PtrToStructure<float>(PluginInterface.Framework.Address.BaseAddress + 0x165C);
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
					gameUIHidden = false;
					this.chatLogObject = IntPtr.Zero;
					return;
				}

				if (chatLogObject == IntPtr.Zero) {
					this.chatLogObject =
						this.getUI2ObjByName(Marshal.ReadIntPtr(this.getBaseUIObj(), 32), "ChatLog", 1);
					return;
				}

				gameUIHidden = Marshal.ReadByte(Marshal.ReadIntPtr(this.chatLogObject, 200) + 115) == 0;
			} catch (Exception ex) {
				PluginLog.LogError(ex.Message);
				gameUIHidden = false;
				this.chatLogObject = IntPtr.Zero;
			}
		}

		public void SetupCommands() {
			PluginInterface.CommandManager.AddHandler("/pfps", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
				HelpMessage = $"Open config window for {this.Name}",
				ShowInHelp = true
			});
		}


		public void OnConfigCommandHandler(object a, object b) {
			if (b is string s && s == "toggle") {
				PluginConfig.Enable = !PluginConfig.Enable;
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
			if (!(gameUIHidden && PluginConfig.HideInCutscene) && PluginConfig.Enable && !string.IsNullOrEmpty(fpsText)) {
				ImGui.SetNextWindowBgAlpha(PluginConfig.Alpha);

				ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse;

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
}
