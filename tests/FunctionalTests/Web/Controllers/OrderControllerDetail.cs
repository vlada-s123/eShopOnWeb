using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class OrderDetailOnGet : IClassFixture<TestApplication>
{
    private const string NotFoundMessage = "No such order found for this user.";
    private readonly TestApplication _factory;

    public OrderDetailOnGet(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ReturnsRedirectToLoginGivenAnonymousUser()
    {
        var orderId = await OrderSeed.AddOrderAsync(_factory, TestApplication.DemoUserEmail, "Anonymous Product", 1m, 1);

        var response = await Client.GetAsync($"/order/detail/{orderId}");

        AssertRedirectToLogin(response);
    }

    [Fact]
    public async Task ShowsOrderLinesAndAddressGivenOwnOrder()
    {
        var user = await _factory.CreateUserAsync();
        var orderId = await OrderSeed.AddOrderAsync(_factory, user.Email!, "Detail Test Product", 12.50m, 3);
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync($"/order/detail/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains($">{orderId}</section>", body);
        Assert.Contains("Detail Test Product", body);
        Assert.Contains("$ 12.50", body);
        Assert.Contains("37.50", body); // total = 3 x 12.50
        Assert.Contains("1 Test Street", body);
        Assert.Contains("Testville", body);
    }

    [Fact]
    public async Task ReturnsBadRequestGivenUnknownOrderId()
    {
        var user = await _factory.CreateUserAsync();
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync("/order/detail/987654");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(NotFoundMessage, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DoesNotRevealAnotherUsersOrder()
    {
        var owner = await _factory.CreateUserAsync();
        var intruder = await _factory.CreateUserAsync();
        var orderId = await OrderSeed.AddOrderAsync(_factory, owner.Email!, "Private Product", 99.99m, 1);
        await LoginAsync(Client, intruder.Email!);

        var response = await Client.GetAsync($"/order/detail/{orderId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(NotFoundMessage, body);
        Assert.DoesNotContain("Private Product", body);
    }
}
