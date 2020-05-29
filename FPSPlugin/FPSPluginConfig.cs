using CheapLoc;
using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Numerics;

namespace FPSPlugin {
	public class FPSPluginConfig : IPluginConfiguration {

		[NonSerialized]
		private DalamudPluginInterface pluginInterface;

		[NonSerialized]
		private FPSPlugin plugin;

		public int Version { get; set; }

		public bool Locked { get; set; }

		public float Alpha { get; set; }

		public bool ShowDecimals { get; set; }

		public Vector4 Colour { get; set; }

		public bool HideInCutscene { get; set; }

		public FPSPluginConfig() {
			LoadDefaults();
		}

		public void LoadDefaults() {
			Colour = new Vector4(0, 1, 1, 1);
			Alpha = 0.5f;
			Locked = false;
			HideInCutscene = true;
			ShowDecimals = false;
		}

		public void Init(FPSPlugin plugin, DalamudPluginInterface pluginInterface) {
			this.plugin = plugin;
			this.pluginInterface = pluginInterface;
		}

		public void Save() {
			pluginInterface.SavePluginConfig(this);
		}

		public bool DrawConfigUI() {
			bool drawConfig = true;
			ImGuiWindowFlags windowFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
			ImGui.Begin($"{plugin.Name} Config", ref drawConfig, windowFlags);

			bool locked = Locked;
			if (ImGui.Checkbox("Lock Display", ref locked)) {
				Locked = locked;
				Save();
			}
			bool hideInCutscene = HideInCutscene;
			if (ImGui.Checkbox("Hide during cutscenes", ref hideInCutscene)) {
				HideInCutscene = hideInCutscene;
				Save();
			}

			bool decimals = ShowDecimals;
			if (ImGui.Checkbox("Show Decimals", ref decimals)) {
				ShowDecimals = decimals;
				Save();
			}

			float bgAlpha = Alpha;
			if (ImGui.SliderFloat("Background Opacity", ref bgAlpha, 0, 1)) {
				Alpha = Math.Max(0, Math.Min(1, bgAlpha));
				Save();
			}

			Vector4 colour = Colour;
			if (ImGui.ColorEdit4("Text Colour", ref colour)) {
				Colour = colour;
				Save();
			}

			ImGui.Separator();
			if (ImGui.Button("Restore Default")) {
				LoadDefaults();
				Save();
			}

			ImGui.End();

			return drawConfig;
		}

	}
}