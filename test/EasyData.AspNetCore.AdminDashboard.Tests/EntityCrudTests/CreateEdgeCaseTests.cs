using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    [Collection("BulkData")]
    public class CreateEdgeCaseTests
    {
        private readonly HttpClient _client;

        public CreateEdgeCaseTests(BulkDataFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        private static string ExtractIdFromRedirect(string locationHeader, string entity)
        {
            var match = Regex.Match(locationHeader, $@"/admin/{entity}/(\d+)/change/");
            Assert.True(match.Success, $"Expected redirect to /admin/{entity}/{{id}}/change/ but got: {locationHeader}");
            return match.Groups[1].Value;
        }

        [Fact]
        public async Task PostCreate_NewIngredient_DoesNotModifyExistingRowsAsync()
        {
            // Arrange
            var existingHtmlBefore = await (await _client.GetAsync("/admin/Ingredient/1/change/")).Content.ReadAsStringAsync();
            Assert.Contains("Ingredient_0001", existingHtmlBefore);

            // Act
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "CreateIsolationTest"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

            var existingHtmlAfter = await (await _client.GetAsync("/admin/Ingredient/1/change/")).Content.ReadAsStringAsync();
            Assert.Contains("Ingredient_0001", existingHtmlAfter);
        }

        [Fact]
        public async Task PostCreate_OneCategory_IncrementsCountByExactlyOneAsync()
        {
            // Arrange — use delta-based assertion to avoid interference from parallel tests
            var htmlBefore = await (await _client.GetAsync("/admin/Category/")).Content.ReadAsStringAsync();
            var countMatch = Regex.Match(htmlBefore, @"(\d+)\s+categor");
            Assert.True(countMatch.Success, "Expected category count in list page");
            var countBefore = int.Parse(countMatch.Groups[1].Value);

            // Act
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "CountTest_" + Guid.NewGuid().ToString("N")[..8]),
                new KeyValuePair<string, string>("Description", "Testing count"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            await _client.PostAsync("/admin/Category/add/", formData);

            // Assert
            var htmlAfter = await (await _client.GetAsync("/admin/Category/")).Content.ReadAsStringAsync();
            var countMatchAfter = Regex.Match(htmlAfter, @"(\d+)\s+categor");
            Assert.True(countMatchAfter.Success);
            var countAfter = int.Parse(countMatchAfter.Groups[1].Value);

            Assert.Equal(countBefore + 1, countAfter);
        }

        [Fact]
        public async Task PostCreate_SaveAndContinue_RedirectsToNewRecordEditFormAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "ContinueTest"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Ingredient/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Matches(@"/admin/Ingredient/\d+/change/", location);

            var html = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains("ContinueTest", html);
        }

        [Fact]
        public async Task PostCreate_RestaurantWithFk_DoesNotModifyReferencedCategoryAsync()
        {
            // Arrange
            var catHtmlBefore = await (await _client.GetAsync("/admin/Category/1/change/")).Content.ReadAsStringAsync();
            Assert.Contains("Italian", catHtmlBefore);

            // Act
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "FkIsolationRestaurant"),
                new KeyValuePair<string, string>("Address", "321 Isolation St"),
                new KeyValuePair<string, string>("CategoryId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            await _client.PostAsync("/admin/Restaurant/add/", formData);

            // Assert
            var catHtmlAfter = await (await _client.GetAsync("/admin/Category/1/change/")).Content.ReadAsStringAsync();
            Assert.Contains("Italian", catHtmlAfter);
        }

        [Fact]
        public async Task PostCreate_InvalidForeignKey_ThrowsAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "BadFkRestaurant"),
                new KeyValuePair<string, string>("Address", "Nowhere"),
                new KeyValuePair<string, string>("CategoryId", "999999"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => _client.PostAsync("/admin/Restaurant/add/", formData));
        }
    }
}
