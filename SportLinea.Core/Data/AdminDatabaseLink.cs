using Microsoft.Data.SqlClient;

namespace SportLinea.Data;

/// <summary>
/// Поднимает админскую БД как окно на клиентскую через синонимы SQL Server.
/// Реальные таблицы остаются в SportLineaDb.
/// </summary>
public static class AdminDatabaseLink
{
    public const string ClientDatabaseName = "SportLineaDb";
    public const string AdminDatabaseName = "SportLineaAdmin";

    private static readonly string[] SharedTables =
    [
        "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims",
        "AspNetRoleClaims", "AspNetUserLogins", "AspNetUserTokens",
        "SportEvents", "Coefficients", "Bets", "AccountOperations",
        "Bonuses", "WithdrawalRequests", "Notifications", "ActionLogs",
        "__EFMigrationsHistory"
    ];

    public static async Task EnsureAsync(string adminConnectionString)
    {
        var master = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = "master"
        };

        await using (var conn = new SqlConnection(master.ConnectionString))
        {
            await conn.OpenAsync();

            if (!await DatabaseExistsAsync(conn, ClientDatabaseName))
            {
                throw new InvalidOperationException(
                    $"Сначала запустите клиентский сайт, чтобы создалась база {ClientDatabaseName}.");
            }

            if (!await DatabaseExistsAsync(conn, AdminDatabaseName))
            {
                await using var create = new SqlCommand(
                    $"CREATE DATABASE [{AdminDatabaseName}]", conn);
                await create.ExecuteNonQueryAsync();
            }
        }

        var admin = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = AdminDatabaseName
        };

        await using (var conn = new SqlConnection(admin.ConnectionString))
        {
            await conn.OpenAsync();
            foreach (var table in SharedTables)
            {
                var sql = $"""
                    IF OBJECT_ID(N'dbo.[{table}]', N'SN') IS NULL
                    AND OBJECT_ID(N'dbo.[{table}]', N'U') IS NULL
                        CREATE SYNONYM dbo.[{table}] FOR [{ClientDatabaseName}].dbo.[{table}];
                    """;
                await using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<bool> DatabaseExistsAsync(SqlConnection conn, string name)
    {
        await using var cmd = new SqlCommand(
            "SELECT 1 FROM sys.databases WHERE name = @name", conn);
        cmd.Parameters.AddWithValue("@name", name);
        var result = await cmd.ExecuteScalarAsync();
        return result is not null;
    }
}
