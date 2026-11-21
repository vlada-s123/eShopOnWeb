using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Microsoft.eShopWeb.FunctionalTests.Web.WebPageHelpers;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class OrderIndexOnGet : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public OrderIndexOnGet(TestApplication factory)
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
        var response = await Client.GetAsync("/order/my-orders");

        AssertRedirectToLogin(response);
    }

    [Fact]
    public async Task ListsOnlyTheSignedInUsersOrders()
    {
        var user = await _factory.CreateUserAsync();
        var otherUser = await _factory.CreateUserAsync();
        var myOrderId = await OrderSeed.AddOrderAsync(_factory, user.Email!, "My Listed Product", 4.25m, 2);
        var otherOrderId = await OrderSeed.AddOrderAsync(_factory, otherUser.Email!, "Someone Elses Product", 9.99m, 1);
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync("/order/my-orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains($"/order/detail/{myOrderId}\"", body);
        Assert.DoesNotContain($"/order/detail/{otherOrderId}\"", body);
        Assert.Contains("8.50", body); // 2 x 4.25
    }

    [Fact]
    public async Task ShowsNoOrdersGivenUserWithoutOrders()
    {
        var user = await _factory.CreateUserAsync();
        await OrderSeed.AddOrderAsync(_factory, TestApplication.DemoUserEmail, "Demo Product", 1m, 1);
        await LoginAsync(Client, user.Email!);

        var response = await Client.GetAsync("/order/my-orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("/order/detail/", await response.Content.ReadAsStringAsync());
    }
}

internal static class OrderSeed
{
    /// <summary>Writes an order straight to the catalog database and returns its id.</summary>
    public static Task<int> AddOrderAsync(TestApplication factory, string buyerEmail, string productName, decimal unitPrice, int units) =>
        factory.InScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<CatalogContext>();
            var address = new Address("1 Test Street", "Testville", "TS", "Testland", "12345");
            var item = new OrderItem(new CatalogItemOrdered(1, productName, "http://test/image.png"), unitPrice, units);
            var order = new Order(buyerEmail, address, new List<OrderItem> { item });
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return order.Id;
        });
}
