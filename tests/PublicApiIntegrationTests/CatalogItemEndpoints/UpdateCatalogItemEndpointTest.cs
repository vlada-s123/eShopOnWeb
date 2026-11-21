using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class UpdateCatalogItemEndpointTest
{
    [TestMethod]
    public async Task ReturnsUpdatedItemAndPersistsChangesGivenValidIdAndAdminUserToken()
    {
        // Update an item this test created, so seeded data other tests rely on is untouched.
        var created = await CatalogItemTestData.CreateItemAsync("ToUpdate");
        var itemId = created.Id;

        // Read the current state so brand/type are preserved and only details change.
        var original = await CatalogItemTestData.GetItemAsync(itemId);

        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var updateBody = new
        {
            id = itemId,
            catalogBrandId = original.CatalogBrandId,
            catalogTypeId = original.CatalogTypeId,
            name = "Updated Integration Test Name",
            description = "Updated integration test description",
            price = 123.45m
        };

        var response = await client.PutAsJsonAsync("api/catalog-items", updateBody);
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<UpdateCatalogItemResponse>();

        Assert.AreEqual(itemId, model!.CatalogItem.Id);
        Assert.AreEqual("Updated Integration Test Name", model.CatalogItem.Name);
        Assert.AreEqual("Updated integration test description", model.CatalogItem.Description);
        Assert.AreEqual(123.45m, model.CatalogItem.Price);
        Assert.AreEqual(original.CatalogBrandId, model.CatalogItem.CatalogBrandId);
        Assert.AreEqual(original.CatalogTypeId, model.CatalogItem.CatalogTypeId);

        // Verify the change was persisted by reading the item back.
        var verifyResponse = await ProgramTest.NewClient.GetAsync($"api/catalog-items/{itemId}");
        verifyResponse.EnsureSuccessStatusCode();
        var verifyString = await verifyResponse.Content.ReadAsStringAsync();
        var verifyModel = verifyString.FromJson<GetByIdCatalogItemResponse>();

        Assert.AreEqual(itemId, verifyModel!.CatalogItem.Id);
        Assert.AreEqual("Updated Integration Test Name", verifyModel.CatalogItem.Name);
        Assert.AreEqual("Updated integration test description", verifyModel.CatalogItem.Description);
        Assert.AreEqual(123.45m, verifyModel.CatalogItem.Price);
        Assert.AreEqual(original.CatalogBrandId, verifyModel.CatalogItem.CatalogBrandId);
        Assert.AreEqual(original.CatalogTypeId, verifyModel.CatalogItem.CatalogTypeId);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidIdAndAdminUserToken()
    {
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var updateBody = new
        {
            id = 0,
            catalogBrandId = 1,
            catalogTypeId = 1,
            name = "Should Not Be Saved",
            description = "Should not be saved",
            price = 1m
        };

        var countBefore = await CountItemsNamedAsync("Should Not Be Saved");

        var response = await client.PutAsJsonAsync("api/catalog-items", updateBody);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(countBefore, await CountItemsNamedAsync("Should Not Be Saved"), "A failed update must not create an item.");
    }

    [TestMethod]
    public async Task ReturnsForbiddenAndKeepsItemGivenNormalUserToken()
    {
        var item = await CatalogItemTestData.CreateItemAsync("NoUpdate");
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var response = await client.PutAsJsonAsync("api/catalog-items", new
        {
            id = item.Id,
            catalogBrandId = item.CatalogBrandId,
            catalogTypeId = item.CatalogTypeId,
            name = "Hijacked",
            description = "Hijacked",
            price = 0.01m
        });

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await CatalogItemTestData.GetItemAsync(item.Id);
        Assert.AreEqual(item.Name, stored.Name);
        Assert.AreEqual(item.Price, stored.Price);
    }

    private static async Task<int> CountItemsNamedAsync(string name)
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-items?pageSize=1000");
        response.EnsureSuccessStatusCode();
        var model = (await response.Content.ReadAsStringAsync()).FromJson<ListPagedCatalogItemResponse>();
        return model!.CatalogItems.Count(i => i.Name == name);
    }
}
