using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        const int itemId = 1;

        // Read the current state so brand/type are preserved and only details change.
        var getResponse = await ProgramTest.NewClient.GetAsync($"api/catalog-items/{itemId}");
        getResponse.EnsureSuccessStatusCode();
        var getString = await getResponse.Content.ReadAsStringAsync();
        var original = getString.FromJson<GetByIdCatalogItemResponse>();

        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var updateBody = new
        {
            id = itemId,
            catalogBrandId = original!.CatalogItem.CatalogBrandId,
            catalogTypeId = original.CatalogItem.CatalogTypeId,
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
        Assert.AreEqual(original.CatalogItem.CatalogBrandId, model.CatalogItem.CatalogBrandId);
        Assert.AreEqual(original.CatalogItem.CatalogTypeId, model.CatalogItem.CatalogTypeId);

        // Verify the change was persisted by reading the item back.
        var verifyResponse = await ProgramTest.NewClient.GetAsync($"api/catalog-items/{itemId}");
        verifyResponse.EnsureSuccessStatusCode();
        var verifyString = await verifyResponse.Content.ReadAsStringAsync();
        var verifyModel = verifyString.FromJson<GetByIdCatalogItemResponse>();

        Assert.AreEqual(itemId, verifyModel!.CatalogItem.Id);
        Assert.AreEqual("Updated Integration Test Name", verifyModel.CatalogItem.Name);
        Assert.AreEqual("Updated integration test description", verifyModel.CatalogItem.Description);
        Assert.AreEqual(123.45m, verifyModel.CatalogItem.Price);
        Assert.AreEqual(original.CatalogItem.CatalogBrandId, verifyModel.CatalogItem.CatalogBrandId);
        Assert.AreEqual(original.CatalogItem.CatalogTypeId, verifyModel.CatalogItem.CatalogTypeId);
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

        var response = await client.PutAsJsonAsync("api/catalog-items", updateBody);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
