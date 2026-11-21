using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class DeleteCatalogItemEndpointTest
{
    [TestMethod]
    public async Task ReturnsSuccessGivenValidIdAndAdminUserToken()
    {
        // Delete an item this test created, so seeded data other tests rely on is untouched.
        var item = await CatalogItemTestData.CreateItemAsync("ToDelete");

        var response = await CatalogItemTestData.AdminClient().DeleteAsync($"api/catalog-items/{item.Id}");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<DeleteCatalogItemResponse>();

        Assert.AreEqual("Deleted", model!.Status);
        var afterDelete = await CatalogItemTestData.GetItemResponseAsync(item.Id);
        Assert.AreEqual(HttpStatusCode.NotFound, afterDelete.StatusCode, "The deleted item must no longer be retrievable.");
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidIdAndAdminUserToken()
    {
        var response = await CatalogItemTestData.AdminClient().DeleteAsync("api/catalog-items/0");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        var verifyModel = await CatalogItemTestData.GetItemAsync(5);
        Assert.AreEqual(5, verifyModel.Id);
        Assert.AreEqual("Roslyn Red Sheet", verifyModel.Name);
    }

    [TestMethod]
    public async Task ReturnsForbiddenAndKeepsItemGivenNormalUserToken()
    {
        var item = await CatalogItemTestData.CreateItemAsync("KeepMe");
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var response = await client.DeleteAsync($"api/catalog-items/{item.Id}");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(item.Name, (await CatalogItemTestData.GetItemAsync(item.Id)).Name);
    }
}
