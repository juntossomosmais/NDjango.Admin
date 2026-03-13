using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityListTests
{
    public class SortingTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public SortingTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task SortByName_Ascending_ReturnsOrderedResultsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?sort=Name&dir=asc");
            var html = await response.Content.ReadAsStringAsync();

            var italianPos = html.IndexOf("Italian");
            var japanesePos = html.IndexOf("Japanese");
            var mexicanPos = html.IndexOf("Mexican");

            Assert.True(italianPos < japanesePos);
            Assert.True(japanesePos < mexicanPos);
        }

        [Fact]
        public async Task SortByName_Descending_ReturnsReverseOrderAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?sort=Name&dir=desc");
            var html = await response.Content.ReadAsStringAsync();

            var italianPos = html.IndexOf("Italian");
            var mexicanPos = html.IndexOf("Mexican");

            Assert.True(mexicanPos < italianPos);
        }

        [Fact]
        public async Task ColumnHeaders_ContainSortLinksAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("sort=Name", html);
            Assert.Contains("dir=asc", html);
        }
    }
}
