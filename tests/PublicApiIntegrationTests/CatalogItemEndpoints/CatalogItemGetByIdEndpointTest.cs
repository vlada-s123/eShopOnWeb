using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
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
        var countBefore = await GetCatalogItemCountAsync();

        var response = await ProgramTest.NewClient.GetAsync("api/catalog-items/0");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        var countAfter = await GetCatalogItemCountAsync();
        Assert.AreEqual(countBefore, countAfter);
    }

    private async Task<int> GetCatalogItemCountAsync()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-items");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<ListPagedCatalogItemResponse>();

        return model!.CatalogItems.Count();
    }
}
