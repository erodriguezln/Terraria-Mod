using bGamesMod.Models;
using bGamesMod.UIKit;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.UI.Elements;

namespace bGamesMod.bGamesModServices
{
	internal class BuffServiceAfectivo : bGamesModService
	{
		private BuffWindowAfectivo _buffWindow;

		public static int[] SkipBuffs = new int[] {
			BuffID.Pygmies, BuffID.LeafCrystal, BuffID.IceBarrier, BuffID.BabySlime, BuffID.Ravens,
			BuffID.BeetleEndurance1, BuffID.BeetleEndurance2, BuffID.BeetleEndurance3,
			BuffID.BeetleMight1, BuffID.BeetleMight2, BuffID.BeetleMight3, BuffID.ImpMinion, BuffID.SpiderMinion,
			BuffID.TwinEyesMinion,
			BuffID.MinecartLeft, BuffID.MinecartLeftMech, BuffID.MinecartLeftWood, BuffID.MinecartRight,
			BuffID.MinecartRightMech, BuffID.MinecartRightWood,
			BuffID.SharknadoMinion, BuffID.UFOMinion, BuffID.DeadlySphere, BuffID.SolarShield1, BuffID.SolarShield2,
			BuffID.SolarShield3, BuffID.StardustDragonMinion,
			BuffID.StardustGuardianMinion, BuffID.HornetMinion, BuffID.PirateMinion, BuffID.StardustMinion
		};

		public BuffServiceAfectivo()
		{
			this._hotbarIcon =
				new UIImage(bGamesMod.instance.Assets.Request<Texture2D>("Images/bGames/afective",
					AssetRequestMode.ImmediateLoad));
			this.HotbarIcon.Tooltip = bGamesMod.HeroText("OpenBuffAfectivo");
			this.HotbarIcon.onLeftClick += HotbarIcon_onLeftClick;

			Login.OnLogout += HandleLogout;
		}

		private void HandleLogout()
		{
			if (_buffWindow != null)
			{
				_buffWindow.Visible = false;
			}
		}

		public override void MyGroupUpdated()
		{
			if (!HasPermissionToUse && _buffWindow != null)
			{
				_buffWindow.Visible = false;
			}
		}

		private void HotbarIcon_onLeftClick(object sender, EventArgs e)
		{
			if (!Login.LoggedIn)
			{
				Main.NewText(bGamesMod.HeroText("BuffsLogin"));
				return;
			}

			if (_buffWindow == null)
			{
				if (!Main.dedServ)
				{
					_buffWindow = new BuffWindowAfectivo(Login.UserID);
					this.AddUIView(_buffWindow);
					_buffWindow.Visible = false;

					_buffWindow.Y = 270;
					_buffWindow.X = 130;
				}
			}

			_buffWindow.Visible = !_buffWindow.Visible;
			if (_buffWindow.Visible)
			{
				_buffWindow.LoadPlayerAttributes(Login.UserID);
			}
		}
	}

	internal class BuffWindowAfectivo : UIWindow
	{
		private UILabel lblPoints;
		private int puntos;
		private string userID;
		private UIScrollView scrollView;

		// buffId es igual a id_modifiable_mechanic_videogame y puebla la ventana en cada apertura
		private Dictionary<int, int> _mechanicsMap = new Dictionary<int, int>();

		// point_dimension_id
		private const int AfectivoDimensionId = 3;

		// id_attributes
		private const int AfectivoAttributeId = 3;

		// "dimension" usada para filtrar las mecanicas que se muestran en la ventana.
		private const string AfectivoDimension = "afectivo";

		public BuffWindowAfectivo(string userID)
		{
			this.userID = userID;
			this.CanMove = true;
			UILabel lTitle = new UILabel(bGamesMod.HeroText("BuffsAfectivo"));
			UILabel lSeconds = new UILabel("Puntos");

			lblPoints = new UILabel("Loading...");
			scrollView = new UIScrollView();
			UIImage bClose = new UIImage(closeTexture);

			lTitle.Scale = .6f;
			lTitle.X = Spacing;
			lTitle.Y = Spacing;
			lTitle.OverridesMouse = false;

			bClose.Y = Spacing;
			bClose.onLeftClick += bClose_onLeftClick;

			lblPoints.Scale = .6f;
			lblPoints.Width = 75;
			lblPoints.Y = lTitle.Y + lTitle.Height;

			scrollView.X = lTitle.X;
			scrollView.Y = lblPoints.Y + lblPoints.Height + Spacing;
			scrollView.Width = 300;
			scrollView.Height = 250;

			this.Width = scrollView.X + scrollView.Width + Spacing;
			this.Height = scrollView.Y + scrollView.Height + Spacing;

			lblPoints.X = Width - lblPoints.Width - Spacing;
			bClose.X = Width - bClose.Width - Spacing;

			lSeconds.Scale = .4f;
			lSeconds.Anchor = AnchorPosition.Right;
			lSeconds.X = lblPoints.X - Spacing;
			lSeconds.Y = lblPoints.Y + lblPoints.Height / 2;

			AddChild(lTitle);
			AddChild(lSeconds);
			AddChild(lblPoints);
			AddChild(scrollView);
			AddChild(bClose);
		}

		// Puebla la ventana de mecanicas con las mecanicas traidas del backend remoto/cliente lsg
		// TODO: Scroll view esta malo, no permite moverlo clickeandolo, solo funciona con scroll wheel
		private void PopulateBuffList(List<MechanicDto> mechanics)
		{
			scrollView.RemoveAllChildren();
			float yPos = Spacing;

			foreach (MechanicDto m in mechanics)
			{
				if (!(m.options is JsonElement opts && opts.ValueKind == JsonValueKind.Object))
				{
					continue;
				}

				if (!opts.TryGetProperty("dimension", out JsonElement dimEl)
				    || !string.Equals(dimEl.GetString(), AfectivoDimension, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (!opts.TryGetProperty("buff_id", out JsonElement buffIdEl)
				    || !buffIdEl.TryGetInt32(out int buffType))
				{
					continue;
				}

				UIRect bg = new UIRect();
				bg.ForegroundColor = buffType % 2 == 0 ? Color.Transparent : Color.Blue * .1f;
				bg.X = Spacing;
				bg.Y = yPos;
				bg.Width = scrollView.Width - 20 - Spacing * 2;
				bg.Tag = buffType;
				string buffDescription = Lang.GetBuffDescription(buffType);
				bg.Tooltip = (buffDescription == null ? "" : buffDescription);
				bg.onLeftClick += bg_onLeftClick;

				UIImage buffImage = new UIImage(TextureAssets.Buff[buffType]);
				buffImage.X = Spacing;
				buffImage.Y = SmallSpacing / 2;
				buffImage.OverridesMouse = false;

				bg.Height = buffImage.Height + SmallSpacing;
				yPos += bg.Height;

				// Use the backend mechanic_name instead of Terraria's Lang string
				UILabel label = new UILabel(m.mechanic_name ?? Lang.GetBuffName(buffType));
				label.Scale = .4f;
				label.Anchor = AnchorPosition.Left;
				label.X = buffImage.X + buffImage.Width + Spacing;
				label.Y = buffImage.Y + buffImage.Height / 2;
				label.OverridesMouse = false;

				bg.AddChild(buffImage);
				bg.AddChild(label);
				scrollView.AddChild(bg);
			}

			scrollView.ContentHeight = yPos;
		}

		public async void LoadPlayerAttributes(string userID)
		{
			try
			{
				int? points = await BuffApiService.GetAttributePointsAsync(userID, AfectivoAttributeId);
				if (points.HasValue)
				{
					puntos = points.Value;
					lblPoints.Text = puntos.ToString();
				}
				else
				{
					Main.NewText($"No se encontró el atributo con ID {AfectivoAttributeId}.");
				}
			}
			catch (Exception ex)
			{
				Main.NewText($"Error loading attributes: {ex.Message}");
			}

			try
			{
				List<MechanicDto> mechanics = await BuffApiService.GetMechanicsAsync(BuffApiService.VideoGameId);

				// Build the redeem map
				_mechanicsMap.Clear();
				foreach (MechanicDto m in mechanics)
				{
					if (m.options is JsonElement opts
					    && opts.ValueKind == JsonValueKind.Object
					    && opts.TryGetProperty("buff_id", out JsonElement buffIdEl)
					    && buffIdEl.TryGetInt32(out int buffId))
					{
						_mechanicsMap[buffId] = m.id_modifiable_mechanic_videogame;
					}
				}

				// Rebuild the scroll view with backend data
				PopulateBuffList(mechanics);
			}
			catch (Exception ex)
			{
				Main.NewText($"Error loading mechanics: {ex.Message}");
			}
		}

		private async void SpendPoints(int buffType, int pointsToSpend)
		{
			// TODO ¿Como podria suceder este error si la unica forma de ver las recompensas es porque vienen del servidor?
			if (!_mechanicsMap.TryGetValue(buffType, out int mechanicVgId))
			{
				Main.NewText("Esta recompensa aún no está registrada en el servidor.");
				return;
			}

			var request = new RedeemRequest {
				MechanicId = mechanicVgId,
				DimensionId = AfectivoDimensionId,
				Amount = pointsToSpend,
				Metadata = new RedeemMetadata { Note = "Buff canjeable - Afectivo" }
			};

			try
			{
				bool success = await BuffApiService.RedeemAsync(Login.UserID, request);
				if (success)
				{
					LoadPlayerAttributes(userID);
				}
				else
				{
					Main.NewText("Error spending points.");
				}
			}
			catch (Exception ex)
			{
				Main.NewText($"Error spending points: {ex.Message}");
			}
		}

		private void bClose_onLeftClick(object sender, EventArgs e)
		{
			this.Visible = false;
		}

		private void bg_onLeftClick(object sender, EventArgs e)
		{
			UIView view = (UIView)sender;
			int buffType = (int)view.Tag;
			int pointsToSpend = 5;

			if (pointsToSpend <= puntos)
			{
				Main.player[Main.myPlayer].AddBuff(buffType, 60 * 60); // 60 seconds
				SpendPoints(buffType, pointsToSpend);
			}
			else
			{
				Main.NewText("Not enough points to spend.");
			}
		}
	}
}