using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

/// <summary>
/// Two-factor authentication endpoints of ManageController. Every test uses its own new user, so
/// enabling/disabling 2FA in one test cannot leak into another.
/// </summary>
[Collection("Sequential")]
public class ManageTwoFactor : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageTwoFactor(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Theory]
    [InlineData("/manage/two-factor-authentication")]
    [InlineData("/manage/disable2fa-warning")]
    [InlineData("/manage/enable-authenticator")]
    [InlineData("/manage/show-recovery-codes")]
    [InlineData("/manage/reset-authenticator-warning")]
    [InlineData("/manage/generate-recovery-codes-warning")]
    public async Task GetReturnsRedirectToLoginGivenAnonymousUser(string url)
    {
        var response = await Client.GetAsync(url);

        AssertRedirectToLogin(response);
    }

    [Theory]
    [InlineData("/manage/disable2fa")]
    [InlineData("/manage/enable-authenticator")]
    [InlineData("/manage/reset-authenticator")]
    [InlineData("/manage/generate-recovery-codes")]
    public async Task PostReturnsRedirectToLoginGivenAnonymousUser(string url)
    {
        var response = await Client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>()));

        AssertRedirectToLogin(response);
    }

    [Fact]
    public async Task TwoFactorPageOffersAuthenticatorGivenUserWithout2fa()
    {
        var email = await LoginNewUserAsync();

        var response = await Client.GetAsync("/manage/two-factor-authentication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Add authenticator app", body);
        Assert.DoesNotContain("Disable 2FA", body);
        Assert.False((await _factory.GetUserAsync(email)).TwoFactorEnabled);
    }

    [Fact]
    public async Task TwoFactorPageOffersDisableGivenUserWith2fa()
    {
        var email = await LoginNewUserAsync();
        await EnableTwoFactorServerSideAsync(email);

        var response = await Client.GetAsync("/manage/two-factor-authentication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Disable 2FA", body);
        Assert.Contains("Reset recovery codes", body);
    }

    [Fact]
    public async Task EnableAuthenticatorGetShowsTheUsersSharedKey()
    {
        var email = await LoginNewUserAsync();

        var response = await Client.GetAsync("/manage/enable-authenticator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var shownKey = GetSharedKey(body);
        var storedKey = await _factory.WithUserManagerAsync(email, (um, u) => um.GetAuthenticatorKeyAsync(u));
        Assert.False(string.IsNullOrEmpty(storedKey), "Viewing the page should provision an authenticator key.");
        Assert.Equal(storedKey, shownKey, ignoreCase: true);
        Assert.Contains("otpauth://totp/eShopOnWeb:", body);
        Assert.False((await _factory.GetUserAsync(email)).TwoFactorEnabled, "GET must not enable 2FA.");
    }

    [Fact]
    public async Task EnableAuthenticatorWithValidCodeEnables2faAndShowsRecoveryCodes()
    {
        var email = await LoginNewUserAsync();
        var page = await (await Client.GetAsync("/manage/enable-authenticator")).Content.ReadAsStringAsync();
        var code = ComputeTotp(GetSharedKey(page));

        var response = await PostFormAsync(Client, "/manage/enable-authenticator",
            GetRequestVerificationToken(page), ("Code", code));

        AssertRedirectTo(response, "/manage/show-recovery-codes");
        var user = await _factory.GetUserAsync(email);
        Assert.True(user.TwoFactorEnabled, "A valid authenticator code should enable 2FA.");
        Assert.Equal(10, await _factory.WithUserManagerAsync(email, (um, u) => um.CountRecoveryCodesAsync(u)));

        // The recovery codes are handed over once via TempData.
        var codesResponse = await Client.GetAsync("/manage/show-recovery-codes");
        Assert.Equal(HttpStatusCode.OK, codesResponse.StatusCode);
        var codes = GetRecoveryCodes(await codesResponse.Content.ReadAsStringAsync());
        Assert.Equal(10, codes.Count);

        var secondView = await Client.GetAsync("/manage/show-recovery-codes");
        AssertRedirectTo(secondView, "/manage/two-factor-authentication");
    }

    [Fact]
    public async Task EnableAuthenticatorWithInvalidCodeShowsErrorAndLeaves2faOff()
    {
        var email = await LoginNewUserAsync();
        var page = await (await Client.GetAsync("/manage/enable-authenticator")).Content.ReadAsStringAsync();
        var wrongCode = ComputeTotp(GetSharedKey(page)) == "000000" ? "111111" : "000000";

        var response = await PostFormAsync(Client, "/manage/enable-authenticator",
            GetRequestVerificationToken(page), ("Code", wrongCode));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Verification code is invalid.", await response.Content.ReadAsStringAsync());
        Assert.False((await _factory.GetUserAsync(email)).TwoFactorEnabled);
        Assert.Equal(0, await _factory.WithUserManagerAsync(email, (um, u) => um.CountRecoveryCodesAsync(u)));
    }

    [Fact]
    public async Task ShowRecoveryCodesRedirectsGivenNoFreshCodes()
    {
        await LoginNewUserAsync();

        var response = await Client.GetAsync("/manage/show-recovery-codes");

        AssertRedirectTo(response, "/manage/two-factor-authentication");
    }

    [Fact]
    public async Task Disable2faWarningShowsConfirmationGivenUserWith2fa()
    {
        var email = await LoginNewUserAsync();
        await EnableTwoFactorServerSideAsync(email);

        var response = await Client.GetAsync("/manage/disable2fa-warning");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("action=\"/manage/disable2fa\"", await response.Content.ReadAsStringAsync());
        Assert.True((await _factory.GetUserAsync(email)).TwoFactorEnabled, "The warning page must not disable 2FA.");
    }

    [Fact]
    public async Task Disable2faWarningFailsGivenUserWithout2fa()
    {
        await LoginNewUserAsync();

        var response = await Client.GetAsync("/manage/disable2fa-warning");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Disable2faTurnsOffTwoFactor()
    {
        var email = await LoginNewUserAsync();
        await EnableTwoFactorServerSideAsync(email);
        var token = await GetFormTokenAsync(Client, "/manage/disable2fa-warning");

        var response = await PostFormAsync(Client, "/manage/disable2fa", token);

        AssertRedirectTo(response, "/manage/two-factor-authentication");
        Assert.False((await _factory.GetUserAsync(email)).TwoFactorEnabled);
    }

    [Fact]
    public async Task ResetAuthenticatorWarningShowsConfirmation()
    {
        var email = await LoginNewUserAsync();
        var keyBefore = await _factory.WithUserManagerAsync(email, (um, u) => um.GetAuthenticatorKeyAsync(u));

        var response = await Client.GetAsync("/manage/reset-authenticator-warning");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("action=\"/manage/reset-authenticator\"", await response.Content.ReadAsStringAsync());
        Assert.Equal(keyBefore, await _factory.WithUserManagerAsync(email, (um, u) => um.GetAuthenticatorKeyAsync(u)));
    }

    [Fact]
    public async Task ResetAuthenticatorRotatesKeyAndDisables2fa()
    {
        var email = await LoginNewUserAsync();
        var keyBefore = await EnableTwoFactorServerSideAsync(email);
        var token = await GetFormTokenAsync(Client, "/manage/reset-authenticator-warning");

        var response = await PostFormAsync(Client, "/manage/reset-authenticator", token);

        AssertRedirectTo(response, "/manage/enable-authenticator");
        var user = await _factory.GetUserAsync(email);
        Assert.False(user.TwoFactorEnabled, "Resetting the authenticator must turn 2FA off.");
        var keyAfter = await _factory.WithUserManagerAsync(email, (um, u) => um.GetAuthenticatorKeyAsync(u));
        Assert.NotEqual(keyBefore, keyAfter);
    }

    [Fact]
    public async Task GenerateRecoveryCodesWarningShowsConfirmationGivenUserWith2fa()
    {
        var email = await LoginNewUserAsync();
        await EnableTwoFactorServerSideAsync(email);

        var response = await Client.GetAsync("/manage/generate-recovery-codes-warning");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("action=\"/manage/generate-recovery-codes\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GenerateRecoveryCodesWarningFailsGivenUserWithout2fa()
    {
        await LoginNewUserAsync();

        var response = await Client.GetAsync("/manage/generate-recovery-codes-warning");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GenerateRecoveryCodesReplacesCodesGivenUserWith2fa()
    {
        var email = await LoginNewUserAsync();
        await EnableTwoFactorServerSideAsync(email);
        var oldCodes = await _factory.WithUserManagerAsync(email,
            (um, u) => um.GenerateNewTwoFactorRecoveryCodesAsync(u, 10));
        var token = await GetFormTokenAsync(Client, "/manage/generate-recovery-codes-warning");

        var response = await PostFormAsync(Client, "/manage/generate-recovery-codes", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newCodes = GetRecoveryCodes(await response.Content.ReadAsStringAsync());
        Assert.Equal(10, newCodes.Count);
        Assert.Empty(newCodes.Intersect(oldCodes!));

        // Only the new codes are redeemable.
        Assert.True(await _factory.WithUserManagerAsync(email, async (um, u) => (await um.RedeemTwoFactorRecoveryCodeAsync(u, newCodes[0])).Succeeded));
        Assert.False(await _factory.WithUserManagerAsync(email, async (um, u) => (await um.RedeemTwoFactorRecoveryCodeAsync(u, oldCodes!.First())).Succeeded));
    }

    [Fact]
    public async Task GenerateRecoveryCodesFailsAndCreatesNoneGivenUserWithout2fa()
    {
        var email = await LoginNewUserAsync();
        var token = await GetFormTokenAsync(Client, "/manage/two-factor-authentication");

        var response = await PostFormAsync(Client, "/manage/generate-recovery-codes", token);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await _factory.WithUserManagerAsync(email, (um, u) => um.CountRecoveryCodesAsync(u)));
    }

    private async Task<string> LoginNewUserAsync()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);
        return user.Email!;
    }

    /// <summary>Turns 2FA on directly in the identity store (after sign-in) and returns the authenticator key.</summary>
    private Task<string?> EnableTwoFactorServerSideAsync(string email) =>
        _factory.WithUserManagerAsync(email, async (um, u) =>
        {
            await um.ResetAuthenticatorKeyAsync(u);
            await um.SetTwoFactorEnabledAsync(u, true);
            return await um.GetAuthenticatorKeyAsync(u);
        });

    private static string GetSharedKey(string enableAuthenticatorPage)
    {
        var match = Regex.Match(enableAuthenticatorPage, "<kbd>([a-z0-9 ]+)</kbd>");
        Assert.True(match.Success, "The enable-authenticator page should display the shared key.");
        return match.Groups[1].Value.Replace(" ", string.Empty).ToUpperInvariant();
    }

    private static List<string> GetRecoveryCodes(string page) =>
        Regex.Matches(page, @"<code[^>]*>\s*([A-Za-z0-9-]+)\s*</code>").Select(m => m.Groups[1].Value).ToList();
}
