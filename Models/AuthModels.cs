using System.Text.Json.Serialization;

namespace bGamesMod.Models
{
	internal class LoginResponse
	{
		[JsonPropertyName("access_token")] public string AccessToken { get; set; }

		[JsonPropertyName("token_type")] public string TokenType { get; set; }
	}

	internal class WhoAmIResponse
	{
		[JsonPropertyName("id_players")] public int IdPlayers { get; set; }

		[JsonPropertyName("name")] public string Name { get; set; }

		[JsonPropertyName("email")] public string Email { get; set; }

		[JsonPropertyName("age")] public int Age { get; set; }

		[JsonPropertyName("roles")] public string[] Roles { get; set; }
	}
}