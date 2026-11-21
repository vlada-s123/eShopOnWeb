using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.UnitTests.Builders;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories.OrderRepositoryTests;

public class GetById : IClassFixture<CatalogDatabaseFixture>
{
    private readonly CatalogDatabaseFixture _database;
    private OrderBuilder OrderBuilder { get; } = new OrderBuilder();

    public GetById(CatalogDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task GetsExistingOrder()
    {
        var orderId = await AddOrderAsync(OrderBuilder.WithDefaultValues());

        await using var context = _database.CreateContext();
        var orderFromRepo = await new EfRepository<Order>(context).GetByIdAsync(orderId);

        Assert.NotNull(orderFromRepo);
        Assert.Equal(orderId, orderFromRepo!.Id);
        Assert.Equal(OrderBuilder.TestBuyerId, orderFromRepo.BuyerId);
        Assert.Equal(new AddressBuilder().TestCity, orderFromRepo.ShipToAddress.City); // owned type is loaded with the order
    }

    [Fact]
    public async Task DoesNotLoadOrderItemsWithoutSpecification()
    {
        // On a relational database GetById loads only the order row. Code that needs the lines must use
        // OrderWithItemsByIdSpec (see GetByIdWithItemsAsync). InMemory used to hide this difference.
        var orderId = await AddOrderAsync(OrderBuilder.WithDefaultValues());

        await using var context = _database.CreateContext();
        var orderFromRepo = await new EfRepository<Order>(context).GetByIdAsync(orderId);

        Assert.NotNull(orderFromRepo);
        Assert.Empty(orderFromRepo!.OrderItems);
    }

    [Fact]
    public async Task ReturnsNullGivenUnknownId()
    {
        await using var context = _database.CreateContext();

        var orderFromRepo = await new EfRepository<Order>(context).GetByIdAsync(int.MaxValue);

        Assert.Null(orderFromRepo);
    }

    private async Task<int> AddOrderAsync(Order order)
    {
        await using var context = _database.CreateContext();
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    }
}
