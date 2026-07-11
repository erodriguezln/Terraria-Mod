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
			UpdateGameReady();

			// Drain pending buff from the HTTP server
			RewardHttpServer.DrainPending();
		}

		// El servidor solo se marca como listo cuando existe un jugador activo y se puede aplicar el buff.
		private static void UpdateGameReady()
		{
			RewardHttpServer.SetGameReady(IsGameReady());
		}

		private static bool IsGameReady()
		{
			if (Main.gameMenu || Main.myPlayer < 0 || Main.myPlayer >= Main.player.Length)
			{
				return false;
			}

			var player = Main.player[Main.myPlayer];
			return player != null && player.active;
		}

		private static RewardApplicationResult ApplyReward(int rewardId)
		{
			if (!IsGameReady())
			{
				ModUtils.DebugText($"Reward '{rewardId}' ignored because the world is not ready.");
				return new RewardApplicationResult(false, "game_not_ready",
					"No game is ready to apply rewards.");
			}

			if (rewardId >= Terraria.ID.BuffID.Count)
			{
				ModUtils.DebugText($"Unknown reward_id={rewardId}. Reward ignored.");
				return new RewardApplicationResult(false, "unknown_reward",
					"Reward is not supported.");
			}

			var player = Main.player[Main.myPlayer];

			player.AddBuff(rewardId, 60 * 60);

			string buffName = Terraria.ID.BuffID.Search.TryGetName(rewardId, out string name)
				? name
				: $"Buff #{rewardId}";

			Main.NewText($"[LifeSync-Games] Buff aplicado: {buffName}", 100, 220, 100);
			ModUtils.DebugText(
				$"[RewardHttpServer] Reward applied: reward_id={rewardId}, buff={buffName}, player={Main.player[Main.myPlayer].name}");

			return new RewardApplicationResult(true);
		}
	}
}
