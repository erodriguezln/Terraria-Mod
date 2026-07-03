using bGamesMod.bGamesModServices;
using System;
using Terraria;
using Terraria.ModLoader;

namespace bGamesMod
{
	internal class bGamesMod : Mod
	{
		public static bGamesMod instance;

		public override void Load()
		{
			try
			{
				instance = this;

				// Start the reward HTTP server
				RewardHttpServer.Start(
					onReward: ApplyReward,
					logger: ModUtils.DebugText
				);
			}
			catch (Exception e)
			{
				ModUtils.DebugText("Load:\n" + e.Message + "\n" + e.StackTrace + "\n");
			}
		}

		public override void Unload()
		{
			RewardHttpServer.Stop();
			instance = null;
		}

		public static void Update()
		{
			// Drain pending buff from the HTTP server
			RewardHttpServer.DrainPending();
		}

		private static void ApplyReward(int rewardId)
		{
			if (rewardId >= Terraria.ID.BuffID.Count) return;

			if (Main.gameMenu || Main.myPlayer < 0 || Main.myPlayer >= Main.player.Length) return;

			var player = Main.player[Main.myPlayer];
			if (player == null || !player.active) return;

			player.AddBuff(rewardId, 60 * 60);

			string buffName = Terraria.ID.BuffID.Search.TryGetName(rewardId, out string name)
				? name
				: $"Buff #{rewardId}";

			Main.NewText($"[LifeSync-Games] Buff aplicado: {buffName}", 100, 220, 100);
			ModUtils.DebugText(
				$"[RewardHttpServer] Reward applied: reward_id={rewardId}, buff={buffName}, player={Main.player[Main.myPlayer].name}");
		}
	}
}