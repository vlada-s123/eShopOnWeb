using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.FunctionalTests.Web;

/// <summary>
/// Stand-in for an external OAuth provider (Google, Microsoft, ...). On challenge it behaves like a
/// provider that immediately approves: it issues the Identity external cookie carrying the
/// authentication properties the app supplied, then redirects back to the app's callback URL.
/// Everything after that (GetExternalLoginInfoAsync, AddLoginAsync, ...) is the real app code.
/// </summary>
public class TestExternalAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestExternal";
    public const string DisplayName = "Test External Provider";

    // Key used by SignInManager.ConfigureExternalAuthenticationProperties to carry the user id.
    private const string XsrfKey = "XsrfId";

    public TestExternalAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    public static string ProviderKeyFor(string userId) => $"external-{userId}";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        properties.Items.TryGetValue(XsrfKey, out var userId);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, ProviderKeyFor(userId ?? "anonymous")),
            new Claim(ClaimTypes.Name, "External Test User")
        }, SchemeName);

        await Context.SignInAsync(IdentityConstants.ExternalScheme, new ClaimsPrincipal(identity), properties);
        Context.Response.Redirect(properties.RedirectUri ?? "/");
    }
}
