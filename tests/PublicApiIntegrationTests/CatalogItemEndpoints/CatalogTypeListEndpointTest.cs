using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogTypeEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class CatalogTypeListEndpointTest
{
    [TestMethod]
    public async Task ReturnsCatalogTypesGivenValidRequest()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-types");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<ListCatalogTypesResponse>();

        Assert.IsNotNull(model);
        Assert.IsTrue(model!.CatalogTypes.Count > 0);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenUnmappedRoute()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-types/0");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
