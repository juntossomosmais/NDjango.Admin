using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.EntityListTests
{
    [Collection("BulkData")]
    public class PaginationTests
    {
        private readonly HttpClient _client;

        // All queries use ?q=Ingredient_ to scope results to the 60 seeded records,
        // isolating pagination assertions from CRUD edge-case tests that share this fixture.
        private const string ScopedListUrl = "/admin/Ingredient/?q=Ingredient_";

        public PaginationTests(BulkDataFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        private static int CountTableBodyRows(string html)
        {
            var tbodyMatch = Regex.Match(html, @"<tbody>(.*?)</tbody>", RegexOptions.Singleline);
            if (!tbodyMatch.Success) return 0;
            return Regex.Matches(tbodyMatch.Groups[1].Value, @"<tr>").Count;
        }

        [Fact]
        public async Task GetIngredientList_Page1With60Records_Returns25RowsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync(ScopedListUrl);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(BulkDataFixture.DefaultPageSize, CountTableBodyRows(html));
        }

        [Fact]
        public async Task GetIngredientList_Page1With60Records_ShowsTotalCountAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync(ScopedListUrl);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains($"{BulkDataFixture.TotalIngredients} ingredients", html);
        }

        [Fact]
        public async Task GetIngredientList_MultiplePages_RendersPaginationLinksAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync(ScopedListUrl);
            var html = await response.Content.ReadAsStringAsync();

            // Assert — 60 records / 25 per page = 3 pages
            Assert.Contains("class=\"pagination\"", html);
            Assert.Contains("class=\"this-page\"", html);
        }

        [Fact]
        public async Task GetIngredientList_Page2With60Records_ShowsDifferentRecordsThanPage1Async()
        {
            // Arrange — default sort is by Id (ascending), so Ingredient_0001 is on page 1
            var page1Response = await _client.GetAsync(ScopedListUrl);
            var page1Html = await page1Response.Content.ReadAsStringAsync();

            // Act
            var page2Response = await _client.GetAsync(ScopedListUrl + "&page=2");
            var page2Html = await page2Response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);
            Assert.Equal(BulkDataFixture.DefaultPageSize, CountTableBodyRows(page2Html));
            Assert.Contains("Ingredient_0001", page1Html);
            Assert.DoesNotContain("Ingredient_0001", page2Html);
        }

        [Fact]
        public async Task GetIngredientList_LastPageWith60Records_Returns10RemainingRowsAsync()
        {
            // Arrange
            var expectedLastPageRecords = BulkDataFixture.TotalIngredients % BulkDataFixture.DefaultPageSize;

            // Act
            var response = await _client.GetAsync(ScopedListUrl + "&page=3");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expectedLastPageRecords, CountTableBodyRows(html));
        }

        [Fact]
        public async Task GetIngredientList_PageBeyondLast_ReturnsZeroRowsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync(ScopedListUrl + "&page=100");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, CountTableBodyRows(html));
        }

        [Fact]
        public async Task GetCategoryList_SinglePageOfRecords_NoPaginationRenderedAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("class=\"pagination\"", html);
        }

        [Fact]
        public async Task GetIngredientList_Page1_HighlightsPage1AsCurrentAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync(ScopedListUrl);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("<span class=\"this-page\">1</span>", html);
        }

        [Fact]
        public async Task GetIngredientList_Page2_HighlightsPage2AsCurrentAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync(ScopedListUrl + "&page=2");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("<span class=\"this-page\">2</span>", html);
        }
    }
}
