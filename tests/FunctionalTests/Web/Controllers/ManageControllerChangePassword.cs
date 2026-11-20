using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageChangePasswordOnPost : IClassFixture<TestApplication>
{
    public ManageChangePasswordOnPost(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ReturnsRedirectGivenAnonymousUser()
    {
        var response = await Client.PostAsync("/manage/changepassword", new FormUrlEncodedContent(new Dictionary<string, string>()));
        var redirectLocation = response!.Headers.Location!.OriginalString;

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", redirectLocation);
    }
}
