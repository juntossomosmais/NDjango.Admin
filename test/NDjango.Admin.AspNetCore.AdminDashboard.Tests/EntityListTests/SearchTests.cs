using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityListTests
{
    public class SearchTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public SearchTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task Search_FiltersResultsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?q=Italian");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Italian", html);
            Assert.DoesNotContain("Japanese", html);
            Assert.DoesNotContain("Mexican", html);
        }

        [Fact]
        public async Task Search_ShowsFilteredCountAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?q=Italian");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("1 category", html);
        }

        [Fact]
        public async Task Search_NoMatch_ShowsZeroResultsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?q=NonExistentValue");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("0 categories", html);
        }
    }
}
