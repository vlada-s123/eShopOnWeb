using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.UnitTests.Builders;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories.OrderRepositoryTests;

public class GetByIdWithItemsAsync : IClassFixture<CatalogDatabaseFixture>
{
    private readonly CatalogDatabaseFixture _database;
    private OrderBuilder OrderBuilder { get; } = new OrderBuilder();

    public GetByIdWithItemsAsync(CatalogDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task GetOrderAndItemsByOrderIdWhenMultipleOrdersPresent()
    {
        //Arrange
        var itemOneUnitPrice = 5.50m;
        var itemOneUnits = 2;
        var itemTwoUnitPrice = 7.50m;
        var itemTwoUnits = 5;

        var firstOrder = OrderBuilder.WithDefaultValues();
        // Each line needs its own CatalogItemOrdered: it is an EF owned type, and sharing one instance
        // between two owners silently drops it from one of them on SQL Server (InMemory hid this).
        var secondOrderItems = new List<OrderItem>
        {
            new OrderItem(NewItemOrdered(), itemOneUnitPrice, itemOneUnits),
            new OrderItem(NewItemOrdered(), itemTwoUnitPrice, itemTwoUnits)
        };
        var secondOrder = OrderBuilder.WithItems(secondOrderItems);

        await using (var arrange = _database.CreateContext())
        {
            arrange.Orders.AddRange(firstOrder, secondOrder);
            await arrange.SaveChangesAsync();
        }
        Assert.NotEqual(firstOrder.Id, secondOrder.Id);

        //Act (fresh context: everything asserted below came back from SQL Server)
        await using var context = _database.CreateContext();
        var spec = new OrderWithItemsByIdSpec(secondOrder.Id);
        var orderFromRepo = await new EfRepository<Order>(context).FirstOrDefaultAsync(spec);

        //Assert
        Assert.NotNull(orderFromRepo);
        Assert.Equal(secondOrder.Id, orderFromRepo!.Id);
        Assert.Equal(2, orderFromRepo.OrderItems.Count);
        Assert.Equal(itemOneUnits, orderFromRepo.OrderItems.Single(x => x.UnitPrice == itemOneUnitPrice).Units);
        Assert.Equal(itemTwoUnits, orderFromRepo.OrderItems.Single(x => x.UnitPrice == itemTwoUnitPrice).Units);
        Assert.All(orderFromRepo.OrderItems, i => Assert.Equal(OrderBuilder.TestProductName, i.ItemOrdered.ProductName));
        Assert.Equal(itemOneUnitPrice * itemOneUnits + itemTwoUnitPrice * itemTwoUnits, orderFromRepo.Total());
    }

    private CatalogItemOrdered NewItemOrdered() =>
        new(OrderBuilder.TestCatalogItemId, OrderBuilder.TestProductName, OrderBuilder.TestPictureUri);
}
