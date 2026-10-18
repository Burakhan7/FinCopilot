using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinCopilot.Infrastructure.Persistence;

internal static class SnakeCaseNaming
{
    public static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var prevIsLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                var prevIsUpper = i > 0 && char.IsUpper(name[i - 1]);
                if (prevIsLowerOrDigit || (prevIsUpper && nextIsLower))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static void ApplySnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());

            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName(table) ?? property.Name));

            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));

            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }
    }
}
