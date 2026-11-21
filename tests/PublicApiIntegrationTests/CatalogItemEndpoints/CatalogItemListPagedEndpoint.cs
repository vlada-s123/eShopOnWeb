using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class CatalogItemListPagedEndpoint
{
    [TestMethod]
    public async Task ReturnsFirst10CatalogItems()
    {
        var all = await GetPageAsync("/api/catalog-items?pageSize=1000");

        var page = await GetPageAsync("/api/catalog-items?pageSize=10");

        Assert.AreEqual(10, page.CatalogItems.Count);
        CollectionAssert.AreEqual(
            all.CatalogItems.Take(10).Select(i => i.Id).ToList(),
            page.CatalogItems.Select(i => i.Id).ToList(),
            "The first page should be the first 10 items of the full list, in order.");
    }

    [TestMethod]
    public async Task ReturnsCorrectCatalogItemsGivenPageIndex1()
    {
        const int pageSize = 10;
        const int pageIndex = 1;
        var all = await GetPageAsync("/api/catalog-items?pageSize=1000");

        var page = await GetPageAsync($"/api/catalog-items?pageSize={pageSize}&pageIndex={pageIndex}");

        // Derived from the full list, so the expectation holds whatever other tests have added.
        var expectedIds = all.CatalogItems.Skip(pageSize * pageIndex).Take(pageSize).Select(i => i.Id).ToList();
        Assert.IsTrue(expectedIds.Count > 0, "Test needs more than one page of catalog items.");
        CollectionAssert.AreEqual(expectedIds, page.CatalogItems.Select(i => i.Id).ToList());
    }

    [TestMethod]
    public async Task FiltersByBrandAndType()
    {
        var page = await GetPageAsync("/api/catalog-items?pageSize=1000&catalogBrandId=2&catalogTypeId=1");

        Assert.IsTrue(page.CatalogItems.Count > 0);
        Assert.IsTrue(page.CatalogItems.All(i => i.CatalogBrandId == 2 && i.CatalogTypeId == 1),
            "Every returned item should match both filters.");
    }

    [DataTestMethod]
    [DataRow("catalog-items", "catalogItems")]
    [DataRow("catalog-brands", "catalogBrands")]
    [DataRow("catalog-types", "catalogTypes")]
    [DataRow("catalog-items/1", "catalogItem")]
    public async Task SuccessFullMutipleParallelCall(string endpointName, string payloadProperty)
    {
        var client = ProgramTest.NewClient;
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(client.GetAsync($"/api/{endpointName}"));
        }
        var responses = await Task.WhenAll(tasks);

        var failures = responses.Where(r => r.StatusCode != HttpStatusCode.OK).Select(r => (int)r.StatusCode).ToList();
        Assert.AreEqual(0, failures.Count, $"Non-200 responses under concurrency: {string.Join(", ", failures)}");

        // Every concurrent caller must get the same, non-empty payload (correlationId differs per call, so compare the payload).
        var payloads = new List<string>();
        foreach (var response in responses)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.IsTrue(json.RootElement.TryGetProperty(payloadProperty, out var payload),
                $"Response from {endpointName} has no '{payloadProperty}' property.");
            Assert.IsTrue(payload.ValueKind == JsonValueKind.Object || payload.GetArrayLength() > 0,
                $"'{payloadProperty}' from {endpointName} should not be empty.");
            payloads.Add(payload.GetRawText());
        }
        Assert.AreEqual(1, payloads.Distinct().Count(), $"Concurrent calls to {endpointName} returned different data.");
    }

    private static async Task<ListPagedCatalogItemResponse> GetPageAsync(string url)
    {
        var response = await ProgramTest.NewClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsStringAsync()).FromJson<ListPagedCatalogItemResponse>()!;
    }
}
