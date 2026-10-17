namespace GreekReaderAPI.Models;

public sealed record User
{
    public long Id { get; init; }
    public string Email { get; init; } = "";
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed record LoginToken
{
    public long Id { get; init; }
    public long UserId { get; init; }
    public byte[] TokenHash { get; init; } = [];
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; init; }
}