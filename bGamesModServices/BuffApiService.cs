using bGamesMod.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace bGamesMod.bGamesModServices
{
	// Handles HTTP communication with the LSG backend
	internal static class BuffApiService
	{
		private const string BaseUrl = "http://localhost:8012";
		internal const int VideoGameId = 8;

		private static HttpClient CreateAuthorizedClient()
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.Authorization =
				new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Login.AccessToken);
			return client;
		}

		// Trae los atributos del jugador y retorna el balance del atributo específico.
		public static async Task<int?> GetAttributePointsAsync(string userId, int attributeId)
		{
			using (var client = CreateAuthorizedClient())
			{
				var attributes = await client.GetFromJsonAsync<List<PlayerAttribute>>(
					$"{BaseUrl}/players/{userId}/attributes/points");

				if (attributes == null)
				{
					return null;
				}

				var attr = attributes.FirstOrDefault(a => a.IdAttributes == attributeId);
				return attr?.BalanceLedger;
			}
		}

		public static async Task<bool> RedeemAsync(string userId, RedeemRequest request)
		{
			using (var client = CreateAuthorizedClient())
			{
				string url = $"{BaseUrl}/videogames/{VideoGameId}/players/{userId}/redeem";
				var response = await client.PostAsJsonAsync(url, request);
				return response.IsSuccessStatusCode;
			}
		}

		// Retorna la lista de mecánicas disponibles para un gameId.
		public static async Task<List<MechanicDto>> GetMechanicsAsync(int gameId)
		{
			using (var client = CreateAuthorizedClient())
			{
				var mechanics = await client.GetFromJsonAsync<List<MechanicDto>>(
					$"{BaseUrl}/videogames/{gameId}/mechanics");

				return mechanics ?? new List<MechanicDto>();
			}
		}
	}
}