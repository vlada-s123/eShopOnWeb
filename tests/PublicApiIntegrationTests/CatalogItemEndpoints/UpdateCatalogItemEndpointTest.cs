using BlazorShared.Models;
using Microsoft.eShopWeb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class UpdateCatalogItemEndpointTest
{
    [TestMethod]
    public async Task ReturnsUpdatedItemGivenValidRequestAndAdminUserToken()
    {
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var jsonContent = GetValidUpdateItemJson(1, "updated name", "updated description", 2.34m, 1, 2);
        var response = await client.PutAsync("api/catalog-items", jsonContent);
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<UpdateCatalogItemResponse>();

        Assert.AreEqual(1, model!.CatalogItem.Id);
        Assert.AreEqual("updated name", model.CatalogItem.Name);
        Assert.AreEqual("updated description", model.CatalogItem.Description);
        Assert.AreEqual(2.34m, model.CatalogItem.Price);
        Assert.AreEqual(1, model.CatalogItem.CatalogBrandId);
        Assert.AreEqual(2, model.CatalogItem.CatalogTypeId);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidIdAndAdminUserToken()
    {
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var jsonContent = GetValidUpdateItemJson(0, "updated name", "updated description", 2.34m, 1, 2);
        var response = await client.PutAsync("api/catalog-items", jsonContent);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private StringContent GetValidUpdateItemJson(int id, string name, string description, decimal price, int catalogBrandId, int catalogTypeId)
    {
        var request = new UpdateCatalogItemRequest()
        {
            Id = id,
            Name = name,
            Description = description,
            Price = price,
            CatalogBrandId = catalogBrandId,
            CatalogTypeId = catalogTypeId
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        return jsonContent;
    }
}
