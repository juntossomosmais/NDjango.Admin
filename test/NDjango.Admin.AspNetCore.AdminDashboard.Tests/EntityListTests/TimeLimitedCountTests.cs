using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityListTests
{
    [Collection("SlowCount")]
    public class TimeLimitedCountTests
    {
        private readonly HttpClient _client;

        public TimeLimitedCountTests(SlowCountFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task GetEntityList_CountExceedsTimeout_ReturnsFallbackCountAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — the interceptor delays COUNT beyond PaginationCountTimeoutMs,
            // so the fallback value (9999999999) should appear instead of the real count.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{SlowCountFixture.ExpectedFallbackCount} categories", html);
        }

        [Fact]
        public async Task GetEntityList_CountExceedsTimeout_StillReturnsDataRowsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — even though the count timed out, the data fetch is separate
            // and should still return the actual rows.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var tbodyMatch = Regex.Match(html, @"<tbody>(.*?)</tbody>", RegexOptions.Singleline);
            Assert.True(tbodyMatch.Success, "Expected a <tbody> in the response");
            var rowCount = Regex.Matches(tbodyMatch.Groups[1].Value, @"<tr>").Count;
            Assert.True(rowCount > 0, "Expected at least one data row despite count timeout");
        }

        [Fact]
        public async Task GetEntityList_CountExceedsTimeout_RendersPaginationForFallbackCountAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — with a fallback count of 9999999999, TotalPages will be huge,
            // so pagination links must be rendered.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("class=\"pagination\"", html);
        }
    }

    [Collection("BulkData")]
    public class TimeLimitedCountHappyPathTests
    {
        private readonly HttpClient _client;

        public TimeLimitedCountHappyPathTests(BulkDataFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task GetEntityList_CountWithinTimeout_ReturnsActualCountAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Ingredient/?q=Ingredient_");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — with small data, COUNT completes well within 200ms,
            // so the actual count should be displayed.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{BulkDataFixture.TotalIngredients} ingredients", html);
        }

        [Fact]
        public async Task GetEntityList_CountWithinTimeout_DoesNotReturnFallbackAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Ingredient/?q=Ingredient_");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — the fallback value must NOT appear when the count succeeds.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("9999999999", html);
        }
    }

    [Collection("DisabledTimeout")]
    public class TimeLimitedCountDisabledTests
    {
        private readonly HttpClient _client;

        public TimeLimitedCountDisabledTests(DisabledTimeoutFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task GetEntityList_TimeoutDisabled_ReturnsActualCountDespiteSlowQueryAsync()
        {
            // Arrange & Act — the interceptor delays COUNT by 100ms,
            // but PaginationCountTimeoutMs is -1 (disabled), so it should complete.
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — actual count (2 categories) should appear, not the fallback.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("2 categories", html);
            Assert.DoesNotContain("9999999999", html);
        }
    }
}
