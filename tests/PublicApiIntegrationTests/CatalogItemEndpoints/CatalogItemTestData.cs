using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

/// <summary>
/// Lets mutating tests work on catalog items they created themselves, instead of editing or deleting
/// seeded items that other tests read.
/// </summary>
public static class CatalogItemTestData
{
    public static HttpClient AdminClient()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetAdminUserToken());
        return client;
    }

    public static async Task<CatalogItemDto> CreateItemAsync(string namePrefix, int brandId = 1, int typeId = 2, decimal price = 9.99m)
    {
        var request = new CreateCatalogItemRequest
        {
            CatalogBrandId = brandId,
            CatalogTypeId = typeId,
            Name = UniqueName(namePrefix),
            Description = $"{namePrefix} description",
            Price = price
        };
        var response = await AdminClient().PostAsJsonAsync("api/catalog-items", request);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, "Test setup: creating a catalog item should succeed.");
        var model = (await response.Content.ReadAsStringAsync()).FromJson<CreateCatalogItemResponse>();
        return model!.CatalogItem;
    }

    /// <summary>A unique item name that fits the 50-character column.</summary>
    public static string UniqueName(string prefix)
    {
        var name = $"{prefix} {Guid.NewGuid():N}";
        return name.Length > 50 ? name[..50] : name;
    }

    public static async Task<HttpResponseMessage> GetItemResponseAsync(int id) =>
        await ProgramTest.NewClient.GetAsync($"api/catalog-items/{id}");

    public static async Task<CatalogItemDto> GetItemAsync(int id)
    {
        var response = await GetItemResponseAsync(id);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"GET api/catalog-items/{id} should succeed.");
        return (await response.Content.ReadAsStringAsync()).FromJson<GetByIdCatalogItemResponse>()!.CatalogItem;
    }
}
