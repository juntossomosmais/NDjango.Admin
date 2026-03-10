using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.EntityListTests
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

            html.Should().Contain("Italian");
            html.Should().NotContain("Japanese");
            html.Should().NotContain("Mexican");
        }

        [Fact]
        public async Task Search_ShowsFilteredCountAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?q=Italian");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("1 category");
        }

        [Fact]
        public async Task Search_NoMatch_ShowsZeroResultsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?q=NonExistentValue");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("0 categories");
        }
    }
}
