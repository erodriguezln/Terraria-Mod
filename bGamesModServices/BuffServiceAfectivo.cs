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

		public BuffServiceAfectivo()
		{
			_hotbarIcon =
				new UIImage(bGamesMod.instance.Assets.Request<Texture2D>("Images/bGames/afective",
					AssetRequestMode.ImmediateLoad));
			HotbarIcon.Tooltip = bGamesMod.HeroText("OpenBuffAfectivo");
			HotbarIcon.onLeftClick += HotbarIcon_onLeftClick;

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

		// Coste por canje en esta dimensión
		private const int AfectivoCost = 5;

		// Duración del buff en segundos (ej. 60 * 60 = 3600s = 1 hora)
		private const int AfectivoBuffDurationSeconds = 60 * 60;

		public BuffWindowAfectivo(string userID)
		{
			this.userID = userID;
			CanMove = true;

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

			Width = scrollView.X + scrollView.Width + Spacing;
			Height = scrollView.Y + scrollView.Height + Spacing;

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
		private void PopulateBuffList(List<MechanicDto> mechanics)
		{
			scrollView.ClearContent();
			float yPos = Spacing;

			foreach (MechanicDto m in mechanics)
			{
				if (!(m.Options is JsonElement opts && opts.ValueKind == JsonValueKind.Object))
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

							// Validación defensiva: evitar indices inválidos en TextureAssets
							if (buffType < 0 || buffType >= TextureAssets.Buff.Length)
							{
								// opcional: logear o ignorar
								continue;
							}

				UIRect bg = new UIRect {
					ForegroundColor = buffType % 2 == 0 ? Color.Transparent : Color.Blue * .1f,
					X = Spacing,
					Y = yPos,
					Width = scrollView.Width - 20 - Spacing * 2,
					Tag = buffType,
					Tooltip = Lang.GetBuffDescription(buffType) ?? string.Empty,
				};

				// Suscribir el event handler bg_onLeftClick al event onLeftClick
				// Esto hace que bg_onLeftClick se ejecute automáticamente cada vez que se clickea este elemento
				bg.onLeftClick += bg_onLeftClick;

				UIImage buffImage = new UIImage(TextureAssets.Buff[buffType]) {
					X = Spacing,
					Y = SmallSpacing / 2,
					OverridesMouse = false,
				};

				bg.Height = buffImage.Height + SmallSpacing;
				yPos += bg.Height;

				// Use the backend mechanic_name instead of Terraria's Lang string
				UILabel label = new UILabel(m.MechanicName ?? Lang.GetBuffName(buffType)) {
					Scale = .4f,
					Anchor = AnchorPosition.Left,
					X = buffImage.X + buffImage.Width + Spacing,
					Y = buffImage.Y + buffImage.Height / 2,
					OverridesMouse = false,
				};

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
					if (m.Options is JsonElement opts
					    && opts.ValueKind == JsonValueKind.Object
					    && opts.TryGetProperty("buff_id", out JsonElement buffIdEl)
					    && buffIdEl.TryGetInt32(out int buffId))
					{
						_mechanicsMap[buffId] = m.IdModifiableMechanicVideogame;
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
			// Chequeo defensivo: evita usar un buff no sincronizado (no deberia suceder dado que poblamos el mapa, quizas resincronización).
			if (!_mechanicsMap.TryGetValue(buffType, out int mechanicVgId))
			{
				Main.NewText("Esta recompensa aún no está registrada en el servidor.");
				return;
			}

			var request = new RedeemRequest {
				MechanicId = mechanicVgId,
				DimensionId = AfectivoDimensionId,
				Amount = pointsToSpend,
				Metadata = new RedeemMetadata { Note = "Buff canjeable - Afectivo" },
			};

			try
			{
				bool success = await BuffApiService.RedeemAsync(Login.UserID, request);
				if (success)
				{
					// Solo se aplica el buff si el canje fue exitoso
					Main.player[Main.myPlayer].AddBuff(buffType, AfectivoBuffDurationSeconds);
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
			Visible = false;
		}

		private void bg_onLeftClick(object sender, EventArgs e)
		{
			// Accedes al objeto que fue clickeado y obtienes buffType desde su propiedad Tag
			int buffType = (int)((UIView)sender).Tag;

			if (AfectivoCost <= puntos)
			{
				SpendPoints(buffType, AfectivoCost);
			}
			else
			{
				Main.NewText("Not enough points to spend.");
			}
		}
	}
}