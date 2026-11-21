using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Repositories.BasketRepositoryTests;

public class SetQuantities : IClassFixture<CatalogDatabaseFixture>
{
    private readonly CatalogDatabaseFixture _database;

    public SetQuantities(CatalogDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task RemoveEmptyQuantities()
    {
        var (basketId, itemIds) = await AddBasketAsync("remove-empty@test.com", (2, 3.40m, 4));

        await SetQuantitiesAsync(basketId, new Dictionary<string, int> { { itemIds[0].ToString(), 0 } });

        await using var verify = _database.CreateContext();
        var basket = await verify.Baskets.Include(b => b.Items).SingleAsync(b => b.Id == basketId);
        Assert.Empty(basket.Items);
        Assert.False(await verify.Set<BasketItem>().AnyAsync(i => i.Id == itemIds[0]),
            "The emptied basket line should be deleted from the database, not just detached.");
    }

    [Fact]
    public async Task PersistsUpdatedQuantitiesAndLeavesOtherLinesAlone()
    {
        var (basketId, itemIds) = await AddBasketAsync("update@test.com", (2, 3.40m, 4), (5, 12m, 1));

        await SetQuantitiesAsync(basketId, new Dictionary<string, int> { { itemIds[0].ToString(), 7 } });

        await using var verify = _database.CreateContext();
        var items = await verify.Set<BasketItem>().Where(i => i.BasketId == basketId).ToListAsync();
        Assert.Equal(7, items.Single(i => i.Id == itemIds[0]).Quantity);
        Assert.Equal(1, items.Single(i => i.Id == itemIds[1]).Quantity);
    }

    private async Task<(int BasketId, List<int> ItemIds)> AddBasketAsync(string buyerId,
        params (int CatalogItemId, decimal Price, int Quantity)[] lines)
    {
        await using var context = _database.CreateContext();
        var basket = new Basket(buyerId);
        foreach (var line in lines)
        {
            basket.AddItem(line.CatalogItemId, line.Price, line.Quantity);
        }
        context.Baskets.Add(basket);
        await context.SaveChangesAsync();
        return (basket.Id, basket.Items.Select(i => i.Id).ToList());
    }

    private async Task SetQuantitiesAsync(int basketId, Dictionary<string, int> quantities)
    {
        await using var context = _database.CreateContext();
        var basketService = new BasketService(new EfRepository<Basket>(context), null);
        var result = await basketService.SetQuantities(basketId, quantities);
        Assert.True(result.IsSuccess, $"SetQuantities should succeed for basket {basketId}.");
    }
}
