using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

/// <summary>
/// POST manage/link-login, GET manage/link-login-callback and POST manage/remove-login, exercised with
/// <see cref="TestExternalAuthHandler"/> standing in for an OAuth provider.
/// </summary>
[Collection("Sequential")]
public class ManageLinkLogins : IClassFixture<TestApplication>
{
    private const string Provider = TestExternalAuthHandler.SchemeName;
    private readonly TestApplication _factory;

    public ManageLinkLogins(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Theory]
    [InlineData("/manage/link-login")]
    [InlineData("/manage/remove-login")]
    public async Task PostReturnsRedirectToLoginGivenAnonymousUser(string url)
    {
        var response = await Client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>()));

        AssertRedirectToLogin(response);
    }

    [Fact]
    public async Task CallbackReturnsRedirectToLoginGivenAnonymousUser()
    {
        var response = await Client.GetAsync("/manage/link-login-callback");

        AssertRedirectToLogin(response);
    }

    [Fact]
    public async Task ExternalLoginsPageOffersConfiguredProvider()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync("/manage/external-logins");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains($"value=\"{Provider}\"", body);
    }

    [Fact]
    public async Task LinkLoginChallengesProviderAndCallbackAddsLogin()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);
        var token = await GetFormTokenAsync(Client, "/manage/external-logins");

        // 1. POST link-login: the app challenges the external provider, which sends us back to the callback.
        var linkResponse = await PostFormAsync(Client, "/manage/link-login", token, ("provider", Provider));
        AssertRedirectTo(linkResponse, "/manage/link-login-callback");

        // 2. GET link-login-callback: the app reads the external identity and stores the login.
        var callbackResponse = await Client.GetAsync(linkResponse.Headers.Location);
        AssertRedirectTo(callbackResponse, "/manage/external-logins");

        var logins = await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.GetLoginsAsync(u));
        var login = Assert.Single(logins);
        Assert.Equal(Provider, login.LoginProvider);
        Assert.Equal(TestExternalAuthHandler.ProviderKeyFor(user.Id), login.ProviderKey);

        // 3. The page now lists it as a current login and no longer offers to add it.
        var page = await (await Client.GetAsync("/manage/external-logins")).Content.ReadAsStringAsync();
        Assert.Contains("The external login was added.", page);
        Assert.Contains($"value=\"{login.ProviderKey}\"", page);
        Assert.DoesNotContain($"name=\"provider\" value=\"{Provider}\"", page);
    }

    [Fact]
    public async Task CallbackWithoutExternalSignInFailsAndAddsNothing()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync("/manage/link-login-callback");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.GetLoginsAsync(u)));
    }

    [Fact]
    public async Task RemoveLoginDeletesLinkedLogin()
    {
        var user = await _factory.CreateUserAsync();
        var providerKey = TestExternalAuthHandler.ProviderKeyFor(user.Id);
        var added = await _factory.WithUserManagerAsync(user.Email!,
            (um, u) => um.AddLoginAsync(u, new UserLoginInfo(Provider, providerKey, TestExternalAuthHandler.DisplayName)));
        Assert.True(added.Succeeded, "Test setup: linking the external login should succeed.");
        await LoginAsync(Client, user.Email!);
        var token = await GetFormTokenAsync(Client, "/manage/external-logins");

        var response = await PostFormAsync(Client, "/manage/remove-login", token,
            ("LoginProvider", Provider),
            ("ProviderKey", providerKey));

        AssertRedirectTo(response, "/manage/external-logins");
        Assert.Empty(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.GetLoginsAsync(u)));
        var page = await (await Client.GetAsync("/manage/external-logins")).Content.ReadAsStringAsync();
        Assert.Contains("The external login was removed.", page);
    }

    [Fact]
    public async Task RemoveLoginReportsErrorAndKeepsLoginGivenMissingFields()
    {
        var user = await _factory.CreateUserAsync();
        var providerKey = TestExternalAuthHandler.ProviderKeyFor(user.Id);
        await _factory.WithUserManagerAsync(user.Email!,
            (um, u) => um.AddLoginAsync(u, new UserLoginInfo(Provider, providerKey, TestExternalAuthHandler.DisplayName)));
        await LoginAsync(Client, user.Email!);
        var token = await GetFormTokenAsync(Client, "/manage/external-logins");

        var response = await PostFormAsync(Client, "/manage/remove-login", token, ("LoginProvider", Provider));

        AssertRedirectTo(response, "/manage/external-logins");
        Assert.Single(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.GetLoginsAsync(u)));
        var page = await (await Client.GetAsync("/manage/external-logins")).Content.ReadAsStringAsync();
        Assert.Contains("The external login could not be removed.", page);
    }
}
