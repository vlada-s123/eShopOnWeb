using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class CatalogItemGetByIdEndpointTest
{
    [TestMethod]
    public async Task ReturnsItemGivenValidId()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-items/5");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<GetByIdCatalogItemResponse>();

        Assert.AreEqual(5, model!.CatalogItem.Id);
        Assert.AreEqual("Roslyn Red Sheet", model.CatalogItem.Name);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidId()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-items/0");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(string.Empty, body, "A missing item must not return any catalog item payload.");
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenDeletedId()
    {
        var item = await CatalogItemTestData.CreateItemAsync("Ephemeral");
        (await CatalogItemTestData.AdminClient().DeleteAsync($"api/catalog-items/{item.Id}")).EnsureSuccessStatusCode();

        var response = await ProgramTest.NewClient.GetAsync($"api/catalog-items/{item.Id}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync());
    }
}
