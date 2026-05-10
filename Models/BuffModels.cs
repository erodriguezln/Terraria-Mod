using System.Text.Json;
using System.Text.Json.Serialization;

namespace bGamesMod.Models
{
	internal class PlayerAttribute
	{
		[JsonPropertyName("attribute_name")] public string name { get; set; }

		[JsonPropertyName("balance_ledger")] public int data { get; set; }

		public int id_players { get; set; }
		public string player_name { get; set; }
		public string player_email { get; set; }
		public int id_attributes { get; set; }
		public int snapshot_points { get; set; }
		public int diff_ledger_minus_snapshot { get; set; }
	}

	internal class RedeemRequest
	{
		[JsonPropertyName("modifiable_mechanic_videogame_id")]
		public int MechanicId { get; set; }

		[JsonPropertyName("point_dimension_id")]
		public int DimensionId { get; set; }

		[JsonPropertyName("amount")] public int Amount { get; set; }

		[JsonPropertyName("metadata")] public RedeemMetadata Metadata { get; set; }
	}

	internal class RedeemMetadata
	{
		[JsonPropertyName("note")] public string Note { get; set; }
	}

	internal class MechanicDto
	{
		public int id_modifiable_mechanic_videogame { get; set; }
		public int id_videogame { get; set; }
		public int id_modifiable_mechanic { get; set; }
		public string mechanic_name { get; set; }
		public string mechanic_description { get; set; }
		public string mechanic_type { get; set; }
		public JsonElement? options { get; set; }
	}
}