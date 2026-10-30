namespace GreekReaderAPI.Models;

public sealed record User
{
    public long Id { get; init; }
    public string Email { get; init; } = "";
    public DateTimeOffset? CreatedAt { get; init; }
}

public sealed record LoginToken
{
    public Guid Id { get; init; }
    public long UserId { get; init; }
    public byte[] TokenHash { get; init; } = [];
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; init; }
}

public sealed record OtpCode
{
    public Guid Id { get; init; }
    public long UserId { get; init; }
    public byte[] CodeHash { get; init; } = [];
    public DateTimeOffset ExpiresAt { get; init; }
    public int Attempts { get; init; }
    public DateTimeOffset? UsedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
