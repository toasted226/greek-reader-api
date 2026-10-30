using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GreekReaderAPI.Data;
using GreekReaderAPI.Security;
using GreekReaderAPI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;

namespace GreekReaderAPI.Endpoints;

public sealed record SignInRequest(
    [property: Required, EmailAddress] string Email
);

public sealed record VerifyRequest(string Token);
public sealed record VerifyOtpRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Code
);

public sealed record MeResponse(long Id, string Email, DateTimeOffset? CreatedAt);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/request", RequestSignIn);
        group.MapPost("/verify", Verify);
        group.MapPost("/logout", Logout)
            .RequireAuthorization();

        group.MapPost("/otp/request", RequestOtpSignIn)
            .RequireRateLimiting("otp-request");
        group.MapPost("/otp/verify", VerifyOtp);

        group.MapGet("/me", Me)
            .RequireAuthorization()
            .Produces<MeResponse>()
            .Produces(StatusCodes.Status401Unauthorized);
        group.MapDelete("/me", DeleteMe)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> Logout
    (
        HttpContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteMe
    (
        ClaimsPrincipal principal,
        IUserRepository users,
        CancellationToken ct
    )
    {
        await users.DeleteAsync(principal.GetUserId(), ct);
        return Results.NoContent();
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> Me
    (
        ClaimsPrincipal principal,
        IUserRepository users,
        CancellationToken ct
    )
    {
        var user = await users.GetAsync(principal.GetUserId(), ct);
        if (user == null)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(new MeResponse(user.Id, user.Email, user.CreatedAt));
    }

    private static async Task<IResult> RequestSignIn
    (
        SignInRequest request,
        IAuthService auth,
        CancellationToken ct
    )
    {
        await auth.RequestSignInAsync(request.Email, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RequestOtpSignIn
    (
        SignInRequest request,
        IAuthService auth,
        CancellationToken ct
    )
    {
        await auth.RequestOtpSignInAsync(request.Email, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Verify
    (
        VerifyRequest req,
        IAuthService auth,
        HttpContext ctx,
        CancellationToken ct
    )
    {
        var result = await auth.VerifyAsync(req.Token, ct);

        switch (result)
        {
            case VerifyResult.Success s:
                await ctx.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    UserPrincipalFactory.Build(s.User),
                    new AuthenticationProperties { IsPersistent = true }
                );
                return Results.NoContent();
            case VerifyResult.RateLimited:
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            default:
                return Results.Unauthorized();
        }
    }

    private static async Task<IResult> VerifyOtp
    (
        VerifyOtpRequest request,
        IAuthService auth,
        HttpContext ctx,
        CancellationToken ct
    )
    {
        var result = await auth.VerifyOtpAsync(request.Email, request.Code, ct);

        switch (result)
        {
            case VerifyResult.Success s:
                await ctx.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    UserPrincipalFactory.Build(s.User),
                    new AuthenticationProperties { IsPersistent = true }
                );
                return Results.NoContent();
            case VerifyResult.RateLimited:
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            default:
                return Results.Unauthorized();
        }
    }
}
