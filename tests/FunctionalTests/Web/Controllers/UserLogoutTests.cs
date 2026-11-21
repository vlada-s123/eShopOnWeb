using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.eShopWeb.Web.Configuration;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class UserLogoutOnPost : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public UserLogoutOnPost(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task IsHarmlessGivenAnonymousUser()
    {
        var response = await Client.PostAsync("/User/Logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await IsAuthenticatedAsync(Client), "An anonymous caller must still be anonymous after logout.");
    }

    [Fact]
    public async Task SignsOutAuthenticatedUser()
    {
        await LoginAsync(Client, TestApplication.DemoUserEmail);
        Assert.True(await IsAuthenticatedAsync(Client), "Test setup: the user should be signed in before logout.");

        var response = await Client.PostAsync("/User/Logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith(ConfigureCookieSettings.IdentifierCookieName + "=;", StringComparison.Ordinal));
        Assert.False(await IsAuthenticatedAsync(Client), "The session should be anonymous after logout.");

        var manage = await Client.GetAsync("/manage/my-account");
        AssertRedirectToLogin(manage);
    }

    [Fact]
    public async Task RevokesTheAuthCookieSoItCannotBeReplayed()
    {
        // Manage cookies by hand so we can hold on to the exact cookie that gets logged out.
        var raw = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        var authCookie = await LoginAndGetAuthCookieAsync(raw);
        Assert.Equal(HttpStatusCode.OK, (await SendWithCookieAsync(raw, HttpMethod.Get, "/manage/my-account", authCookie)).StatusCode);

        var logout = await SendWithCookieAsync(raw, HttpMethod.Post, "/User/Logout", authCookie);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        // Replaying the pre-logout cookie must be rejected by the server, not just forgotten by the browser.
        var replay = await SendWithCookieAsync(raw, HttpMethod.Get, "/manage/my-account", authCookie);
        AssertRedirectToLogin(replay);
    }

    private static async Task<bool> IsAuthenticatedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/user");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("isAuthenticated").GetBoolean();
    }

    private static Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, HttpMethod method, string url, string cookie)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }

    /// <summary>Logs in on a client that does not handle cookies and returns the issued auth cookie ("name=value").</summary>
    private static async Task<string> LoginAndGetAuthCookieAsync(HttpClient raw)
    {
        var loginPage = await raw.GetAsync("/identity/account/login");
        var antiforgeryCookie = string.Join("; ", loginPage.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        var token = GetRequestVerificationToken(await loginPage.Content.ReadAsStringAsync());
        var login = new HttpRequestMessage(HttpMethod.Post, "/identity/account/login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = TestApplication.DemoUserEmail,
                ["Password"] = TestApplication.DefaultPassword,
                [TokenTag] = token
            })
        };
        login.Headers.Add("Cookie", antiforgeryCookie);
        var loginResponse = await raw.SendAsync(login);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return loginResponse.Headers.GetValues("Set-Cookie")
            .Select(c => c.Split(';')[0])
            .Single(c => c.StartsWith(ConfigureCookieSettings.IdentifierCookieName + "=", StringComparison.Ordinal));
    }
}
