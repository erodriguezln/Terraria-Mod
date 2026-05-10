using bGamesMod.Models;
using System;
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

		public static async Task<LoginResponse> LoginAsync(string username, string password)
		{
			using (var client = new HttpClient())
			{
				var postData = new Dictionary<string, string> {
					{ "username", username },
					{ "password", password }
				};
				var content = new FormUrlEncodedContent(postData);

				var response = await client.PostAsync($"{BaseUrl}/login", content);

				if (!response.IsSuccessStatusCode)
				{
					return null;
				}

				return await response.Content.ReadFromJsonAsync<LoginResponse>();
			}
		}

		public static async Task<WhoAmIResponse> WhoAmIAsync(string accessToken)
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization =
					new AuthenticationHeaderValue("Bearer", accessToken);

				return await client.GetFromJsonAsync<WhoAmIResponse>($"{BaseUrl}/whoami");
			}
		}
	}
}