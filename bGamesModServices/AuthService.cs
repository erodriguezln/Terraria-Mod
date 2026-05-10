using bGamesMod.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace bGamesMod.bGamesModServices
{
	internal static class AuthService
	{
		private const string BaseUrl = "http://localhost:8001";

		// HttpClient se puede usar desde varios threads y conviene reutilizarlo
		// para evitar crear conexiones nuevas en cada petición.
		private static readonly HttpClient _client = new HttpClient();

		public static async Task<LoginResponse> LoginAsync(string username, string password)
		{
			var postData = new Dictionary<string, string> {
				{ "username", username },
				{ "password", password }
			};

			var response = await _client.PostAsync($"{BaseUrl}/login", new FormUrlEncodedContent(postData));
			if (!response.IsSuccessStatusCode)
				return null;

			return await response.Content.ReadFromJsonAsync<LoginResponse>();
		}

		public static async Task<WhoAmIResponse> WhoAmIAsync(string accessToken)
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/whoami");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			var response = await _client.SendAsync(request);
			return await response.Content.ReadFromJsonAsync<WhoAmIResponse>();
		}
	}
}