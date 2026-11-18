using System.Net;
using System.Text.Json;
using BlazorShared.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class UserControllerGetCurrentUser : IClassFixture<TestApplication>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public UserControllerGetCurrentUser(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ReturnsAnonymousUserInfoGivenAnonymousUser()
    {
        var response = await Client.GetAsync("/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stringResponse = await response.Content.ReadAsStringAsync();
        var userInfo = JsonSerializer.Deserialize<UserInfo>(stringResponse, JsonOptions);

        Assert.NotNull(userInfo);
        Assert.False(userInfo!.IsAuthenticated);
        Assert.DoesNotContain("demouser@microsoft.com", stringResponse);
    }

    [Fact]
    public async Task ReturnsAuthenticatedUserInfoGivenSignedInUser()
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

        // Current user
        var response = await Client.GetAsync("/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stringResponse = await response.Content.ReadAsStringAsync();
        var userInfo = JsonSerializer.Deserialize<UserInfo>(stringResponse, JsonOptions);

        Assert.NotNull(userInfo);
        Assert.True(userInfo!.IsAuthenticated);
        Assert.False(string.IsNullOrEmpty(userInfo.Token));
        Assert.NotNull(userInfo.Claims);
        Assert.NotEmpty(userInfo.Claims);
        Assert.Contains("demouser@microsoft.com", stringResponse);
    }
}
