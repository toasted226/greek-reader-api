using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

public static class TokenGenerator
{
    public static (string Raw, byte[] Hash) Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return (Base64UrlTextEncoder.Encode(bytes), SHA256.HashData(bytes));
    }

    public static byte[]? Hash(string raw)
    {
        try { return SHA256.HashData(Base64UrlTextEncoder.Decode(raw)); }
        catch (FormatException) { return null; }
    }
}