namespace GreekReaderAPI.Email;

public sealed record Email
{
    public string To { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string PlainText { get; init; } = string.Empty;
    public string Html { get; init; } = string.Empty;
}
