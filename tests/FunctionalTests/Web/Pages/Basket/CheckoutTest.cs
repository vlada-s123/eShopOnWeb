using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Pages.Basket;

[Collection("Sequential")]
public class CheckoutTest : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public CheckoutTest(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task SucessfullyPay()
    {

        // Load Home Page
        var response = await Client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();

        // Add Item to Cart
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("id", "2"),
            new KeyValuePair<string, string>("name", "shirt"),
            new KeyValuePair<string, string>("price", "19.49"),
            new KeyValuePair<string, string>(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(stringResponse))
        };
        var formContent = new FormUrlEncodedContent(keyValues);
        var postResponse = await Client.PostAsync("/basket/index", formContent);
        postResponse.EnsureSuccessStatusCode();
        var stringPostResponse = await postResponse.Content.ReadAsStringAsync();
        Assert.Contains(".NET Black &amp; White Mug", stringPostResponse);

        //Load login page
        var loginResponse = await Client.GetAsync("/Identity/Account/Login");
        var longinKeyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("email", "demouser@microsoft.com"),
            new KeyValuePair<string, string>("password", "Pass@word1"),
            new KeyValuePair<string, string>(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(await loginResponse.Content.ReadAsStringAsync()))
        };
        var loginFormContent = new FormUrlEncodedContent(longinKeyValues);
        var loginPostResponse = await Client.PostAsync("/Identity/Account/Login?ReturnUrl=%2FBasket%2FCheckout", loginFormContent);
        var loginStringResponse = await loginPostResponse.Content.ReadAsStringAsync();

        //Basket checkout (Pay now)
        var checkOutKeyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Items[0].Id", "2"),
            new KeyValuePair<string, string>("Items[0].Quantity", "1"),
            new KeyValuePair<string, string>(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(loginStringResponse))
        };
        var checkOutContent = new FormUrlEncodedContent(checkOutKeyValues);     
        var checkOutResponse = await Client.PostAsync("/basket/checkout", checkOutContent);
        var stringCheckOutResponse = await checkOutResponse.Content.ReadAsStringAsync();

        Assert.Contains("/Basket/Success", checkOutResponse.RequestMessage!.RequestUri!.ToString());
        Assert.Contains("Thanks for your Order!", stringCheckOutResponse);

        // The order must actually be persisted for the buyer, with the basket line copied onto it.
        var orders = await _factory.InScopeAsync(sp => sp.GetRequiredService<CatalogContext>().Orders
            .Include(o => o.OrderItems)
            .Where(o => o.BuyerId == TestApplication.DemoUserEmail)
            .ToListAsync());
        var order = Assert.Single(orders);
        var line = Assert.Single(order.OrderItems);
        Assert.Equal(2, line.ItemOrdered.CatalogItemId);
        Assert.Equal(".NET Black & White Mug", line.ItemOrdered.ProductName);
        Assert.Equal(1, line.Units);
        Assert.Equal(8.50m, line.UnitPrice);
        Assert.Equal(8.50m, order.Total());

        // ... the basket is emptied ...
        var basketCount = await _factory.InScopeAsync(sp => sp.GetRequiredService<CatalogContext>().Baskets
            .Include(b => b.Items)
            .Where(b => b.BuyerId == TestApplication.DemoUserEmail)
            .SelectMany(b => b.Items)
            .CountAsync());
        Assert.Equal(0, basketCount);

        // ... and the order shows up in the buyer's order history.
        var myOrders = await (await Client.GetAsync("/order/my-orders")).Content.ReadAsStringAsync();
        Assert.Contains($"/order/detail/{order.Id}\"", myOrders);
    }
}
