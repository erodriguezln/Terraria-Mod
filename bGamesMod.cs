using bGamesMod.bGamesModServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

// TODO, freeze is bypassable.
// TODO, regions prevent all the chest movement and right click.
// TODO -- Should I have all services use the same Global hooks?
namespace bGamesMod
{
	internal record CallButtonParameters(string permissionName, Asset<Texture2D> texture, Action buttonClickedAction, Action<bool> groupUpdated, Func<string> tooltip);

	internal record PermissionsParameters(string permissionName, string permissionDisplayName, Action<bool> groupUpdated);

	internal class bGamesMod : Mod
	{
		public static bGamesMod instance;

		internal List<CallButtonParameters> callButtons = new();
		internal List<PermissionsParameters> permissionsParameters = new();

		internal Dictionary<string, Action<bool>> crossModGroupUpdated = new Dictionary<string, Action<bool>>();

		public override void Load()
		{
			try
			{
				instance = this;

				Init_Load();

				// Start the reward HTTP server
				RewardHttpServer.Start(
					onReward: ApplyBuff,
					logger:   ModUtils.DebugText
				);
			}
			catch (Exception e)
			{
				ModUtils.DebugText("Load:\n" + e.Message + "\n" + e.StackTrace + "\n");
			}
		}

		internal static string HeroText(string key)
		{
			return Language.GetTextValue($"Mods.bGamesMod.{key}");
		}

		private static bool _prevGameMenu = true;

		public static RenderTarget2D RenderTarget { get; set; }

		public static void Init_Load()
		{
			ModUtils.Init();
		}

		public void RegisterButton(string permissionName, Asset<Texture2D> texture, Action buttonClickedAction, Action<bool> groupUpdated, Func<string> tooltip)
		{
			if (!Main.dedServ)
			{
				callButtons.Add(new(permissionName, texture, buttonClickedAction,groupUpdated, tooltip));
			}
		}

		public static void Update(/*GameTime gameTime*/)
		{
			// Drain pending buff from the HTTP server
			RewardHttpServer.DrainPending();

			if (ModUtils.NetworkMode != NetworkMode.Server)
			{
				ModUtils.PreviousKeyboardState = Main.keyState;
				ModUtils.PreviousMouseState = ModUtils.MouseState;
				ModUtils.MouseState = Mouse.GetState();

				ModUtils.SetDeltaTime(/*gameTime*/);
				ModUtils.Update();

			}
		}

		public static void GameLeft()
		{
			ModUtils.DebugText("Game left");
		}

		private static void ApplyBuff(int buffId)
		{
			if (buffId >= Terraria.ID.BuffID.Count) return;
			if (Main.gameMenu || Main.myPlayer < 0 || Main.myPlayer >= Main.player.Length) return;

			var player = Main.player[Main.myPlayer];
			if (player == null || !player.active) return;

			player.AddBuff(buffId, 60 * 60);

			string buffName = Terraria.ID.BuffID.Search.TryGetName(buffId, out string name)
				? name : $"Buff #{buffId}";

			Main.NewText($"[bGamesMod] Buff aplicado: {buffName}", 100, 220, 100);
			ModUtils.DebugText($"[RewardHttpServer] Buff {buffId} ({buffName}) → {Main.player[Main.myPlayer].name}");
		}
	}
}