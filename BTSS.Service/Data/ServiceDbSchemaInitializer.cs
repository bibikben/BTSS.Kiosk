using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BTSS.Service.Data;

public static class ServiceDbSchemaInitializer
{
    public static async Task InitializeAsync(ServiceDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var connection = (SqliteConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await EnsureSftpImportFilesAsync(connection, cancellationToken);
        await EnsureSftpImportIncidentsAsync(connection, cancellationToken);
    }

    private static async Task EnsureSftpImportFilesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "SftpImportFiles", cancellationToken))
        {
            await CreateSftpImportFilesTableAsync(connection, cancellationToken);
            return;
        }

        var columns = await GetColumnsAsync(connection, "SftpImportFiles", cancellationToken);
        var requiredColumns = new[]
        {
            "Id",
            "RemotePath",
            "FileName",
            "SizeBytes",
            "RemoteLastWriteUtc",
            "ContentHash",
            "FirstSeenUtc",
            "LastSeenUtc",
            "LastProcessedUtc",
            "Status",
            "AttemptCount",
            "Error"
        };

        if (requiredColumns.All(columns.Contains))
        {
            await CreateSftpImportFilesIndexesAsync(connection, cancellationToken);
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, @"
ALTER TABLE SftpImportFiles RENAME TO SftpImportFiles_legacy;", transaction, cancellationToken);

        await CreateSftpImportFilesTableAsync(connection, cancellationToken, transaction);

        await ExecuteNonQueryAsync(connection, @"
INSERT INTO SftpImportFiles
(
    Id,
    RemotePath,
    FileName,
    SizeBytes,
    RemoteLastWriteUtc,
    ContentHash,
    FirstSeenUtc,
    LastSeenUtc,
    LastProcessedUtc,
    Status,
    AttemptCount,
    Error
)
SELECT
    Id,
    COALESCE(RemotePath, ''),
    COALESCE(FileName, ''),
    0,
    RemoteModifiedUtc,
    COALESCE(ContentHash, ''),
    COALESCE(LastSeenUtc, CURRENT_TIMESTAMP),
    COALESCE(LastSeenUtc, CURRENT_TIMESTAMP),
    ImportedAtUtc,
    COALESCE(Status, 'new'),
    0,
    Error
FROM SftpImportFiles_legacy;", transaction, cancellationToken);

        await ExecuteNonQueryAsync(connection, @"DROP TABLE SftpImportFiles_legacy;", transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task EnsureSftpImportIncidentsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "SftpImportIncidents", cancellationToken))
        {
            await CreateSftpImportIncidentsTableAsync(connection, cancellationToken);
            return;
        }

        var columns = await GetColumnsAsync(connection, "SftpImportIncidents", cancellationToken);
        var requiredColumns = new[]
        {
            "Id",
            "SftpImportFileId",
            "IncidentId",
            "ImportedAtUtc",
            "ApiPosted",
            "ApiStatusCode",
            "Error"
        };

        if (requiredColumns.All(columns.Contains))
        {
            await CreateSftpImportIncidentsIndexesAsync(connection, cancellationToken);
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, @"
ALTER TABLE SftpImportIncidents RENAME TO SftpImportIncidents_legacy;", transaction, cancellationToken);

        await CreateSftpImportIncidentsTableAsync(connection, cancellationToken, transaction);

        await ExecuteNonQueryAsync(connection, @"
INSERT INTO SftpImportIncidents
(
    Id,
    SftpImportFileId,
    IncidentId,
    ImportedAtUtc,
    ApiPosted,
    ApiStatusCode,
    Error
)
SELECT
    Id,
    SftpImportFileId,
    COALESCE(IncidentId, ''),
    COALESCE(ImportedAtUtc, CURRENT_TIMESTAMP),
    0,
    NULL,
    Error
FROM SftpImportIncidents_legacy;", transaction, cancellationToken);

        await ExecuteNonQueryAsync(connection, @"DROP TABLE SftpImportIncidents_legacy;", transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CreateSftpImportFilesTableAsync(SqliteConnection connection, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS SftpImportFiles (
    Id INTEGER NOT NULL CONSTRAINT PK_SftpImportFiles PRIMARY KEY AUTOINCREMENT,
    RemotePath TEXT NOT NULL,
    FileName TEXT NOT NULL,
    SizeBytes INTEGER NOT NULL DEFAULT 0,
    RemoteLastWriteUtc TEXT NULL,
    ContentHash TEXT NOT NULL,
    FirstSeenUtc TEXT NOT NULL,
    LastSeenUtc TEXT NOT NULL,
    LastProcessedUtc TEXT NULL,
    Status TEXT NOT NULL,
    AttemptCount INTEGER NOT NULL DEFAULT 0,
    Error TEXT NULL
);", transaction, cancellationToken);

        await CreateSftpImportFilesIndexesAsync(connection, cancellationToken, transaction);
    }

    private static async Task CreateSftpImportFilesIndexesAsync(SqliteConnection connection, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        await ExecuteNonQueryAsync(connection, @"
CREATE UNIQUE INDEX IF NOT EXISTS IX_SftpImportFiles_RemotePath
ON SftpImportFiles (RemotePath);", transaction, cancellationToken);

        await ExecuteNonQueryAsync(connection, @"
CREATE INDEX IF NOT EXISTS IX_SftpImportFiles_Status_LastSeenUtc
ON SftpImportFiles (Status, LastSeenUtc);", transaction, cancellationToken);
    }

    private static async Task CreateSftpImportIncidentsTableAsync(SqliteConnection connection, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS SftpImportIncidents (
    Id INTEGER NOT NULL CONSTRAINT PK_SftpImportIncidents PRIMARY KEY AUTOINCREMENT,
    SftpImportFileId INTEGER NOT NULL,
    IncidentId TEXT NOT NULL,
    ImportedAtUtc TEXT NOT NULL,
    ApiPosted INTEGER NOT NULL DEFAULT 0,
    ApiStatusCode INTEGER NULL,
    Error TEXT NULL,
    CONSTRAINT FK_SftpImportIncidents_SftpImportFiles_SftpImportFileId
        FOREIGN KEY (SftpImportFileId) REFERENCES SftpImportFiles (Id)
        ON DELETE CASCADE
);", transaction, cancellationToken);

        await CreateSftpImportIncidentsIndexesAsync(connection, cancellationToken, transaction);
    }

    private static async Task CreateSftpImportIncidentsIndexesAsync(SqliteConnection connection, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        await ExecuteNonQueryAsync(connection, @"
CREATE UNIQUE INDEX IF NOT EXISTS IX_SftpImportIncidents_SftpImportFileId_IncidentId
ON SftpImportIncidents (SftpImportFileId, IncidentId);", transaction, cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    private static async Task<HashSet<string>> GetColumnsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}');";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
