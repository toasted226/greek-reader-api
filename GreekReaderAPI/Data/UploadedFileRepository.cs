using System.IO.Compression;
using System.Text;
using Dapper;
using GreekReaderAPI.Models;
using Npgsql;

namespace GreekReaderAPI.Data;

public sealed class UploadedFileRepository(NpgsqlDataSource db) : IUploadedFileRepository
{
    private static byte[] Compress(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    private static string Decompress(byte[] compressed)
    {
        using var ms = new MemoryStream(compressed);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public async Task<UploadedFile> CreateAsync(UploadedFile file, string content, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var created = await conn.QuerySingleAsync<UploadedFile>(
            new CommandDefinition(
                """
                INSERT INTO uploaded_files (user_id, display_name, original_file_name, file_size_bytes)
                VALUES (@UserId, @DisplayName, @OriginalFileName, @FileSizeBytes)
                RETURNING id, user_id AS UserId, display_name AS DisplayName, original_file_name AS OriginalFileName, file_size_bytes AS FileSizeBytes, uploaded_at AS UploadedAt
                """,
                new
                {
                    UserId = file.UserId,
                    DisplayName = file.DisplayName,
                    OriginalFileName = file.OriginalFileName,
                    FileSizeBytes = file.FileSizeBytes
                },
                transaction: tx,
                cancellationToken: ct));

        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO uploaded_file_contents (file_id, content_compressed)
                VALUES (@FileId, @ContentCompressed)
                """,
                new { FileId = created.Id, ContentCompressed = Compress(content) },
                transaction: tx,
                cancellationToken: ct));

        await tx.CommitAsync(ct);
        return created;
    }

    public async Task<UploadedFile[]> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var results = await conn.QueryAsync<UploadedFile>(
            new CommandDefinition(
                """
                SELECT id, user_id AS UserId, display_name AS DisplayName, original_file_name AS OriginalFileName, file_size_bytes AS FileSizeBytes, uploaded_at AS UploadedAt
                FROM uploaded_files
                WHERE user_id = @UserId
                ORDER BY uploaded_at DESC
                """,
                new { UserId = userId },
                cancellationToken: ct));

        return results.ToArray();
    }

    public async Task<(UploadedFile File, string Content)?> GetWithContentAsync(long fileId, long userId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var result = await conn.QuerySingleOrDefaultAsync<(UploadedFile File, byte[] ContentCompressed)>(
            new CommandDefinition(
                """
                SELECT 
                    f.id, f.user_id AS UserId, f.display_name AS DisplayName, f.original_file_name AS OriginalFileName, 
                    f.file_size_bytes AS FileSizeBytes, f.uploaded_at AS UploadedAt,
                    c.content_compressed AS ContentCompressed
                FROM uploaded_files f
                INNER JOIN uploaded_file_contents c ON c.file_id = f.id
                WHERE f.id = @FileId AND f.user_id = @UserId
                """,
                new { FileId = fileId, UserId = userId },
                cancellationToken: ct));

        if (result.File == default)
            return null;

        var content = Decompress(result.ContentCompressed);
        return (result.File, content);
    }

    public async Task<bool> UpdateDisplayNameAsync(long fileId, long userId, string displayName, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE uploaded_files
                SET display_name = @DisplayName
                WHERE id = @FileId AND user_id = @UserId
                """,
                new { FileId = fileId, UserId = userId, DisplayName = displayName },
                cancellationToken: ct));

        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long fileId, long userId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM uploaded_files
                WHERE id = @FileId AND user_id = @UserId
                """,
                new { FileId = fileId, UserId = userId },
                cancellationToken: ct));

        return rows > 0;
    }
}
