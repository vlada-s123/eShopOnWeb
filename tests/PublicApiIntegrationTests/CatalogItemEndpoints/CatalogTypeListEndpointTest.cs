using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogTypeEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
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
        CollectionAssert.AreEquivalent(
            new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" },
            model!.CatalogTypes.Select(t => t.Name).ToArray());
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenUnmappedRoute()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-types/0");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.IsFalse(body.Contains("catalogTypes"), $"An unmapped route must not fall through to the list endpoint. Body: {body}");
    }
}
