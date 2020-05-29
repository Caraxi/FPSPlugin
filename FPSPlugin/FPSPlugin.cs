using Dalamud.Game.Internal;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace FPSPlugin {
	public class FPSPlugin : IDalamudPlugin {

		public string Name => "FPS Plugin";
		public DalamudPluginInterface PluginInterface { get; private set; }
		public FPSPluginConfig PluginConfig { get; private set; }

		private bool drawConfigWindow = false;

		private long[] history;
		private int c = 0;
		private double lastFps = 0;
		private int MAX_SIZE = 30;
		private bool GameUIHidden = false;
		private Stopwatch sw;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr GetBaseUIObjDelegate();
		[UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
		private delegate IntPtr GetUI2ObjByNameDelegate(IntPtr getBaseUIObj, string UIName, int index = 1);
		private GetBaseUIObjDelegate getBaseUIObj;
		private GetUI2ObjByNameDelegate getUI2ObjByName;

		private IntPtr chatLogObject;

		public void Dispose() {
			PluginInterface.UiBuilder.OnBuildUi -= this.BuildUI;
			PluginInterface.UiBuilder.OnOpenConfigUi -= this.OnConfigCommandHandler;
			PluginInterface.Framework.OnUpdateEvent -= this.OnFrameworkUpdate;
			RemoveCommands();
		}

		public void Initialize(DalamudPluginInterface pluginInterface) {
			this.PluginInterface = pluginInterface;
			this.PluginConfig = (FPSPluginConfig)pluginInterface.GetPluginConfig() ?? new FPSPluginConfig();
			this.PluginConfig.Init(this, pluginInterface);

			history = new long[MAX_SIZE];
			sw = new Stopwatch();
			sw.Start();

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

		private void OnFrameworkUpdate(Framework framework)
		{
			// https://github.com/karashiiro/PingPlugin
			if (this.PluginInterface.ClientState.LocalPlayer == null)
			{
				GameUIHidden = false;
				this.chatLogObject = IntPtr.Zero;
				return;
			}

			if (chatLogObject == IntPtr.Zero)
			{
				this.chatLogObject = this.getUI2ObjByName(Marshal.ReadIntPtr(this.getBaseUIObj(), 32), "ChatLog", 1);
				return;
			}
			GameUIHidden = Marshal.ReadByte(Marshal.ReadIntPtr(this.chatLogObject, 200) + 115) == 0;
		}

		public void SetupCommands() {

			PluginInterface.CommandManager.AddHandler("/pfps", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
				HelpMessage = $"Open config window for {this.Name}",
				ShowInHelp = true
			});

		}


		public void OnConfigCommandHandler(object a, object b) {
			drawConfigWindow = true;
		}

		public void RemoveCommands() {
			PluginInterface.CommandManager.RemoveHandler("/pfps");

		}

		private void BuildUI() {
			
			long t = sw.ElapsedTicks;

			history[c++] = t;

			if (c == MAX_SIZE) {
				c = 0;
				lastFps = 10000000 / history.Average();
			}

			sw.Restart();
			drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();
			if (!(GameUIHidden && PluginConfig.HideInCutscene)) {

				ImGui.SetNextWindowBgAlpha(PluginConfig.Alpha);

				ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse;

				if (PluginConfig.Locked) {
					flags |= ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoMove;
				}

				ImGui.Begin("FPS", flags);
				if (PluginConfig.ShowDecimals) {
					ImGui.TextColored(PluginConfig.Colour, $"FPS: {lastFps:F2}");
				} else {
					ImGui.TextColored(PluginConfig.Colour, $"FPS: {lastFps:F0}");
				}

				ImGui.End();

			}
			
		}
	}

}
