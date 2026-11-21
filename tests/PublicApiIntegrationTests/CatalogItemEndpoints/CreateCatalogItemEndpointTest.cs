using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class CreateCatalogItemEndpointTest
{
    private int _testBrandId = 1;
    private int _testTypeId = 2;
    private string _testDescription = "test description";
    private string _testName = "test name";
    private decimal _testPrice = 1.23m;


    [TestMethod]
    public async Task ReturnsNotAuthorizedGivenNormalUserToken()
    {
        var jsonContent = GetValidNewItemJson();
        var token = ApiTokenHelper.GetNormalUserToken();
        var countBefore = await GetCatalogItemCountAsync();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync("api/catalog-items", jsonContent);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var countAfter = await GetCatalogItemCountAsync();
        Assert.AreEqual(countBefore, countAfter);
    }

    [TestMethod]
    public async Task ReturnsSuccessGivenValidNewItemAndAdminUserToken()
    {
        var jsonContent = GetValidNewItemJson();
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsync("api/catalog-items", jsonContent);
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<CreateCatalogItemResponse>();

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual(_testBrandId, model!.CatalogItem.CatalogBrandId);
        Assert.AreEqual(_testTypeId, model.CatalogItem.CatalogTypeId);
        Assert.AreEqual(_testDescription, model.CatalogItem.Description);
        Assert.AreEqual(_testName, model.CatalogItem.Name);
        Assert.AreEqual(_testPrice, model.CatalogItem.Price);
        Assert.AreEqual($"api/catalog-items/{model.CatalogItem.Id}", response.Headers.Location?.OriginalString);

        // Read it back independently: the item must really be stored, not just echoed.
        var stored = await CatalogItemTestData.GetItemAsync(model.CatalogItem.Id);
        Assert.AreEqual(_testName, stored.Name);
        Assert.AreEqual(_testDescription, stored.Description);
        Assert.AreEqual(_testPrice, stored.Price);
        Assert.AreEqual(_testBrandId, stored.CatalogBrandId);
        Assert.AreEqual(_testTypeId, stored.CatalogTypeId);
    }

    [TestMethod]
    public async Task ReturnsConflictAndCreatesNothingGivenDuplicateName()
    {
        var existing = await CatalogItemTestData.CreateItemAsync("Duplicate");
        var countBefore = await GetCatalogItemCountAsync();
        var request = new CreateCatalogItemRequest
        {
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = _testDescription,
            Name = existing.Name,
            Price = _testPrice
        };

        var response = await CatalogItemTestData.AdminClient().PostAsync("api/catalog-items",
            new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual(countBefore, await GetCatalogItemCountAsync());
    }

    [TestMethod]
    public async Task ReturnsUnauthorizedGivenNoToken()
    {
        var countBefore = await GetCatalogItemCountAsync();

        var response = await ProgramTest.NewClient.PostAsync("api/catalog-items", GetValidNewItemJson());

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(countBefore, await GetCatalogItemCountAsync());
    }

    private async Task<int> GetCatalogItemCountAsync()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-items?pageSize=1000");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<ListPagedCatalogItemResponse>();

        return model!.CatalogItems.Count();
    }

    private StringContent GetValidNewItemJson()
    {
        var request = new CreateCatalogItemRequest()
        {
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = _testDescription,
            Name = _testName,
            Price = _testPrice
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        return jsonContent;
    }
}
