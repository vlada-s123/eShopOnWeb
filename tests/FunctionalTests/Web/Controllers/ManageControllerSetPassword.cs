using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageSetPasswordOnPost : IClassFixture<TestApplication>
{
    private const string Url = "/manage/set-password";
    private readonly TestApplication _factory;

    public ManageSetPasswordOnPost(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ReturnsRedirectGivenAnonymousUser()
    {
        var response = await Client.PostAsync(Url, new FormUrlEncodedContent(new Dictionary<string, string>()));

        AssertRedirectToLogin(response);
    }

    [Fact]
    public async Task GetReturnsRedirectToLoginGivenAnonymousUser()
    {
        var response = await Client.GetAsync(Url);

        AssertRedirectToLogin(response);
    }

    [Fact]
    public async Task GetRedirectsToChangePasswordGivenUserThatAlreadyHasPassword()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync(Url);

        AssertRedirectTo(response, "/manage/change-password");
    }

    [Fact]
    public async Task GetReturnsSetPasswordFormGivenUserWithoutPassword()
    {
        var email = await LoginThenRemovePasswordAsync();

        var response = await Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"NewPassword\"", body);
        Assert.DoesNotContain("name=\"OldPassword\"", body);
        Assert.False(await _factory.WithUserManagerAsync(email, (um, u) => um.HasPasswordAsync(u)),
            "Viewing the form must not set a password.");
    }

    [Fact]
    public async Task SetsPasswordGivenUserWithoutPassword()
    {
        const string newPassword = "S3t-Passw0rd!";
        var email = await LoginThenRemovePasswordAsync();
        var token = await GetFormTokenAsync(Client, Url);

        var response = await PostFormAsync(Client, Url, token,
            ("NewPassword", newPassword),
            ("ConfirmPassword", newPassword));

        AssertRedirectTo(response, Url);
        Assert.True(await _factory.WithUserManagerAsync(email, (um, u) => um.CheckPasswordAsync(u, newPassword)),
            "The password set through the form should be stored for the user.");

        // Now that the user has a password, set-password hands over to change-password.
        AssertRedirectTo(await Client.GetAsync(Url), "/manage/change-password");

        var freshClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(freshClient, email, newPassword);
    }

    [Fact]
    public async Task RedisplaysFormAndSetsNothingGivenMismatchedConfirmation()
    {
        var email = await LoginThenRemovePasswordAsync();
        var token = await GetFormTokenAsync(Client, Url);

        var response = await PostFormAsync(Client, Url, token,
            ("NewPassword", "S3t-Passw0rd!"),
            ("ConfirmPassword", "Other-Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("do not match", await response.Content.ReadAsStringAsync());
        Assert.False(await _factory.WithUserManagerAsync(email, (um, u) => um.HasPasswordAsync(u)));
    }

    /// <summary>
    /// Signs in a new user, then removes their password server-side (as if they had only an external login).
    /// The existing auth cookie stays valid, which is what lets us reach the set-password form.
    /// </summary>
    private async Task<string> LoginThenRemovePasswordAsync()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);
        var removed = await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.RemovePasswordAsync(u));
        Assert.True(removed.Succeeded, "Test setup: removing the password should succeed.");
        return user.Email!;
    }
}
