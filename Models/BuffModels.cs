using System.Text.Json;
using System.Text.Json.Serialization;

namespace bGamesMod.Models
{
	internal class PlayerAttribute
	{
		[JsonPropertyName("attribute_name")] public string AttributeName { get; set; }

		[JsonPropertyName("balance_ledger")] public int BalanceLedger { get; set; }

		[JsonPropertyName(("id_players"))] public int IdPlayers { get; set; }

		[JsonPropertyName("player_name")] public string PlayerName { get; set; }

		[JsonPropertyName("player_email")] public string PlayerEmail { get; set; }

		[JsonPropertyName("id_attributes")] public int IdAttributes { get; set; }

		[JsonPropertyName("snapshot_points")] public int SnapshotPoints { get; set; }

		[JsonPropertyName("diff_ledger_minus_snapshot")] public int DiffLedgerMinusSnapshot { get; set; }
	}

	internal class RedeemRequest
	{
		[JsonPropertyName("modifiable_mechanic_videogame_id")] public int MechanicId { get; set; }

		[JsonPropertyName("point_dimension_id")] public int DimensionId { get; set; }

		[JsonPropertyName("amount")] public int Amount { get; set; }

		[JsonPropertyName("metadata")] public RedeemMetadata Metadata { get; set; }
	}

	internal class RedeemMetadata
	{
		[JsonPropertyName("note")] public string Note { get; set; }
	}

	internal class MechanicDto
	{
		[JsonPropertyName("id_modifiable_mechanic_videogame")] public int IdModifiableMechanicVideogame { get; set; }

		[JsonPropertyName("id_videogame")] public int IdVideogame { get; set; }

		[JsonPropertyName("id_modifiable_mechanic")] public int IdModifiableMechanic { get; set; }

		[JsonPropertyName("mechanic_name")] public string MechanicName { get; set; }

		[JsonPropertyName("mechanic_description")] public string MechanicDescription { get; set; }

		[JsonPropertyName("mechanic_type")] public string MechanicType { get; set; }

		[JsonPropertyName("options")] public JsonElement? Options { get; set; }
	}
}