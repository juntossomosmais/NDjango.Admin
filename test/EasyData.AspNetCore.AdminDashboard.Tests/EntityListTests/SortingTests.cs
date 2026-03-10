using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.EntityListTests
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

            italianPos.Should().BeLessThan(japanesePos);
            japanesePos.Should().BeLessThan(mexicanPos);
        }

        [Fact]
        public async Task SortByName_Descending_ReturnsReverseOrderAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?sort=Name&dir=desc");
            var html = await response.Content.ReadAsStringAsync();

            var italianPos = html.IndexOf("Italian");
            var mexicanPos = html.IndexOf("Mexican");

            mexicanPos.Should().BeLessThan(italianPos);
        }

        [Fact]
        public async Task ColumnHeaders_ContainSortLinksAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("sort=Name");
            html.Should().Contain("dir=asc");
        }
    }
}
