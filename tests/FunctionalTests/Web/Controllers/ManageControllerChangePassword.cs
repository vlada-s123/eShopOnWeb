using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageChangePasswordOnPost : IClassFixture<TestApplication>
{
    private const string Url = "/manage/change-password";
    private readonly TestApplication _factory;

    public ManageChangePasswordOnPost(TestApplication factory)
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
    public async Task GetReturnsChangePasswordFormGivenUserWithPassword()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"OldPassword\"", body);
        Assert.Contains("name=\"NewPassword\"", body);
        Assert.Contains("name=\"ConfirmPassword\"", body);
    }

    [Fact]
    public async Task ChangesPasswordGivenCorrectCurrentPassword()
    {
        const string newPassword = "N3w-Passw0rd!";
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);
        var token = await GetFormTokenAsync(Client, Url);

        var response = await PostFormAsync(Client, Url, token,
            ("OldPassword", TestApplication.DefaultPassword),
            ("NewPassword", newPassword),
            ("ConfirmPassword", newPassword));

        AssertRedirectTo(response, Url);
        var page = await (await Client.GetAsync(Url)).Content.ReadAsStringAsync();
        Assert.Contains("Your password has been changed.", page);

        // Persisted: the new password works, the old one no longer does.
        Assert.True(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.CheckPasswordAsync(u, newPassword)),
            "The new password should be stored for the user.");
        Assert.False(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.CheckPasswordAsync(u, TestApplication.DefaultPassword)),
            "The old password should no longer be valid.");

        var freshClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(freshClient, user.Email!, newPassword);
    }

    [Fact]
    public async Task RedisplaysFormAndKeepsPasswordGivenWrongCurrentPassword()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);
        var token = await GetFormTokenAsync(Client, Url);

        var response = await PostFormAsync(Client, Url, token,
            ("OldPassword", "Wrong-Passw0rd!"),
            ("NewPassword", "N3w-Passw0rd!"),
            ("ConfirmPassword", "N3w-Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Incorrect password.", await response.Content.ReadAsStringAsync());
        Assert.True(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.CheckPasswordAsync(u, TestApplication.DefaultPassword)),
            "A failed change must leave the original password in place.");
    }

    [Fact]
    public async Task RedisplaysFormGivenMismatchedConfirmation()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);
        var token = await GetFormTokenAsync(Client, Url);

        var response = await PostFormAsync(Client, Url, token,
            ("OldPassword", TestApplication.DefaultPassword),
            ("NewPassword", "N3w-Passw0rd!"),
            ("ConfirmPassword", "Different-Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("do not match", await response.Content.ReadAsStringAsync());
        Assert.True(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.CheckPasswordAsync(u, TestApplication.DefaultPassword)));
    }

    [Fact]
    public async Task RejectsPostWithoutAntiForgeryToken()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);

        var response = await Client.PostAsync(Url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["OldPassword"] = TestApplication.DefaultPassword,
            ["NewPassword"] = "N3w-Passw0rd!",
            ["ConfirmPassword"] = "N3w-Passw0rd!"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await _factory.WithUserManagerAsync(user.Email!, (um, u) => um.CheckPasswordAsync(u, TestApplication.DefaultPassword)));
    }
}
