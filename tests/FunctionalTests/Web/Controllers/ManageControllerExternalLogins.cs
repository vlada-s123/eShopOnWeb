using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageExternalLoginsOnGet : IClassFixture<TestApplication>
{
    public ManageExternalLoginsOnGet(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ReturnsRedirectToLoginGivenAnonymousUser()
    {
        var response = await Client.GetAsync("/manage/external-logins");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ReturnsExternalLoginsPageGivenAuthenticatedUser()
    {
        // Login
        var getResponse = await Client.GetAsync("/identity/account/login");
        getResponse.EnsureSuccessStatusCode();
        var loginPage = await getResponse.Content.ReadAsStringAsync();
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Email", "demouser@microsoft.com"),
            new KeyValuePair<string, string>("Password", "Pass@word1"),
            new KeyValuePair<string, string>(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(loginPage))
        };
        var loginResponse = await Client.PostAsync("/identity/account/login", new FormUrlEncodedContent(keyValues));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        // External logins page
        var response = await Client.GetAsync("/manage/external-logins");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));

        // The GET must not change account state: the profile page still shows the same user.
        var profileResponse = await Client.GetAsync("/manage/my-account");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profileBody = await profileResponse.Content.ReadAsStringAsync();
        Assert.Contains("demouser@microsoft.com", profileBody);
    }
}
