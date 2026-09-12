using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SmartQuote.API.Shared.Infrastructure.Persistence;

/// <summary>
/// Renames tables, columns, keys and indexes to snake_case so PostgreSQL identifiers
/// match Claude.md's convention, without pulling in an extra naming-convention package.
/// </summary>
public static class SnakeCaseNamingConventionExtensions
{
    public static void UseSnakeCaseNamingConvention(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));

            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));

            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));

            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()!));

            foreach (var index in entity.GetIndexes())
            {
                var databaseName = index.GetDatabaseName();
                if (databaseName is not null)
                    index.SetDatabaseName(ToSnakeCase(databaseName));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (char.IsUpper(current))
            {
                if (i > 0 && (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
