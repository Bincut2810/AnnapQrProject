using System.Text;
using System.Text.Json;
using Annap.CoffeeQrOrdering.Application;
using Annap.CoffeeQrOrdering.Domain.ValueObjects;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;

namespace Annap.CoffeeQrOrdering.Infrastructure.Sommelier;

/// <summary>Nearest available menu items by cosine distance in PostgreSQL (pgvector).</summary>
public sealed class SommelierVectorRetriever(AppDbContext db, ILogger<SommelierVectorRetriever> logger)
{
    private static readonly JsonSerializerOptions SensoryJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<SommelierMenuCandidate>> RetrieveNearestAsync(
        float[] queryEmbedding,
        int take,
        string? beverageFamilyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding.Length == 0 || take <= 0)
            return [];

        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        var shouldClose = conn.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync(cancellationToken);

        try
        {
            var familyKey = BeverageFamilyGrounding.NormalizeFamilyKey(beverageFamilyKey);
            var sql = new StringBuilder(
                """
                SELECT mi."Id", mi."Name", mi."TastingNotes", mi."MoodProfile", mi."Price", mc."Name" AS "CategoryName",
                       mi."SensoryProfile"::text AS "SensoryJson",
                       mi."CaffeineLevel", mi."SweetnessLevel", mi."AcidityLevel"
                FROM menu_items AS mi
                INNER JOIN menu_categories AS mc ON mc."Id" = mi."CategoryId"
                WHERE mi."IsAvailable" = TRUE AND mi."IsArchived" = FALSE AND mi."Embedding" IS NOT NULL
                """);

            var allowedCategories = familyKey is null
                ? Array.Empty<string>()
                : BeverageFamilyGrounding.AllowedCategoryNames(familyKey).ToArray();
            if (allowedCategories.Length > 0)
                sql.AppendLine("""AND lower(mc."Name") = ANY(@allowedCategories)""");
            if (familyKey == BeverageFamilyGrounding.Matcha)
                sql.AppendLine("""AND (lower(mi."Name") LIKE '%matcha%' OR lower(COALESCE(mi."IngredientBreakdown", '')) LIKE '%matcha%' OR lower(COALESCE(mi."FlavorTags", '')) LIKE '%matcha%')""");
            else if (familyKey == BeverageFamilyGrounding.Tea)
                sql.AppendLine("""AND lower(mi."Name") NOT LIKE '%matcha%' AND lower(COALESCE(mi."IngredientBreakdown", '')) NOT LIKE '%matcha%' AND lower(COALESCE(mi."FlavorTags", '')) NOT LIKE '%matcha%'""");

            sql.AppendLine(
                """
                ORDER BY mi."Embedding" <=> @q
                LIMIT @lim
                """);

            await using var cmd = new NpgsqlCommand(sql.ToString(), conn);
            cmd.Parameters.Add(new NpgsqlParameter("q", new Vector(queryEmbedding)) { DataTypeName = "vector" });
            cmd.Parameters.Add(new NpgsqlParameter("lim", take));
            if (allowedCategories.Length > 0)
            {
                cmd.Parameters.Add(new NpgsqlParameter("allowedCategories", allowedCategories.Select(x => x.ToLowerInvariant()).ToArray()));
            }

            var list = new List<SommelierMenuCandidate>(take);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                DrinkSensoryProfile? sp = null;
                if (!reader.IsDBNull(6))
                {
                    var json = reader.GetString(6);
                    if (!string.IsNullOrWhiteSpace(json) && json != "{}")
                    {
                        try
                        {
                            sp = JsonSerializer.Deserialize<DrinkSensoryProfile>(json, SensoryJson);
                        }
                        catch
                        {
                            sp = null;
                        }
                    }
                }

                list.Add(new SommelierMenuCandidate(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetString(5),
                    sp,
                    reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    reader.IsDBNull(9) ? null : reader.GetInt32(9)));
            }

            return list;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "pgvector retrieval failed; sommelier will fall back.");
            return [];
        }
        finally
        {
            if (shouldClose)
                await conn.CloseAsync();
        }
    }
}
