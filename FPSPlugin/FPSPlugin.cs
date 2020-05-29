using Dalamud.Game.Internal.Network;
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

		public void Dispose() {
			PluginInterface.UiBuilder.OnBuildUi -= this.BuildUI;
			PluginInterface.UiBuilder.OnOpenConfigUi -= this.OnConfigCommandHandler;
			PluginInterface.Framework.Network.OnNetworkMessage -= this.OnNetworkHandler;
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
			PluginInterface.Framework.Network.OnNetworkMessage += this.OnNetworkHandler;
		}

		private void OnNetworkHandler(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction) {
			// https://github.com/karashiiro/PingPlugin
			const ushort eventPlay = 0x02C3;
			const ushort eventFinish = 0x0239;
			if (opCode == eventPlay)
            {
                var packetData = Marshal.PtrToStructure<EventPlay>(dataPtr);
                
                if ((packetData.Flags & 0x00000400) != 0)
                    GameUIHidden = true;
            }
            else if (opCode == eventFinish)
            {
                GameUIHidden = false;
            }
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
