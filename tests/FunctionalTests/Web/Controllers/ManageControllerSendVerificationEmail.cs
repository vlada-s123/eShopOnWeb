using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageSendVerificationEmailOnPost : IClassFixture<TestApplication>
{
    private const string Url = "/manage/send-verification-email";
    private readonly TestApplication _factory;

    public ManageSendVerificationEmailOnPost(TestApplication factory)
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
        var sentBefore = _factory.Emails.Sent.Count;

        var response = await Client.PostAsync(Url, new FormUrlEncodedContent(new Dictionary<string, string>()));

        AssertRedirectToLogin(response);
        Assert.Equal(sentBefore, _factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task SendsWorkingConfirmationLinkGivenAuthenticatedUser()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);
        var token = await GetFormTokenAsync(Client, "/manage/my-account");

        var response = await PostFormAsync(Client, Url, token,
            ("Username", user.UserName!),
            ("Email", user.Email!));

        AssertRedirectTo(response, "/manage/my-account");
        var profile = await (await Client.GetAsync("/manage/my-account")).Content.ReadAsStringAsync();
        Assert.Contains("Verification email sent. Please check your email.", profile);

        var email = Assert.Single(_factory.Emails.Sent, e => e.Email == user.Email);
        Assert.Equal("Confirm your email", email.Subject);

        // The link in the email must actually confirm the address.
        var link = Regex.Match(email.Message, "href='([^']+)'").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(link), $"No confirmation link in email body: {email.Message}");
        Assert.False((await _factory.GetUserAsync(user.Email!)).EmailConfirmed);

        var confirmResponse = await Client.GetAsync(System.Net.WebUtility.HtmlDecode(link));

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.True((await _factory.GetUserAsync(user.Email!)).EmailConfirmed,
            "Following the emailed link should mark the email as confirmed.");
    }
}
