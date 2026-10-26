using bGamesMod.bGamesModServices;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace bGamesMod
{
	public class bGamesModSystem : ModSystem
	{
		public override void OnWorldUnload()
		{
			// Se ejecuta inmediatamente al abandonar una partida, antes de volver al menú.
			RewardHttpServer.SetGameReady(false);
			RewardHttpServer.DrainPendingAndCancel();
		}

		public override void UpdateUI(Microsoft.Xna.Framework.GameTime gameTime)
		{
			// UpdateUI también se ejecuta durante las actualizaciones del menú.
			// Así /health y la cola siguen sincronizados.
			UpdateRewardServer("UpdateUI");
		}

		public override void PostUpdateEverything()
		{
			UpdateRewardServer("PostUpdateEverything");
		}

		private static void UpdateRewardServer(string source)
		{
			try
			{
				bGamesMod.Update();
			}
			catch (Exception e)
			{
				ModUtils.DebugText(source + " Error: " + e.Message + e.StackTrace);
			}
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int inventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (inventoryLayerIndex != -1)
			{
				layers.Insert(inventoryLayerIndex, new LegacyGameInterfaceLayer(
					"HerosMod: UI",
					delegate {
						return true;
					},
					InterfaceScaleType.UI)
				);
			}

			int rulerLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Ruler"));
			if (rulerLayerIndex != -1)
			{
				layers.Insert(rulerLayerIndex, new LegacyGameInterfaceLayer(
					"HerosMod: Game Scale UI",
					delegate {
						return true;
					},
					InterfaceScaleType.Game)
				);
			}
		}
	}
}
