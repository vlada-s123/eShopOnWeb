using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class UserLogoutOnPost : IClassFixture<TestApplication>
{
    public UserLogoutOnPost(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ReturnsRedirectOrUnauthorizedGivenAnonymousUser()
    {
        var response = await Client.PostAsync("/User/Logout", null);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Unexpected status code {response.StatusCode}");

        var location = response.Headers.Location?.ToString() ?? string.Empty;
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.DoesNotContain("Logout", location, StringComparison.OrdinalIgnoreCase);
        }
        Assert.False(response.Headers.Contains("Set-Cookie") &&
            response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.OrdinalIgnoreCase)) && false);
    }
}
