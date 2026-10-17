using System.Security.Claims;
using GreekReaderAPI.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GreekReaderAPI.Security;

public static class UserPrincipalFactory
{
    public static ClaimsPrincipal Build(User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }
}