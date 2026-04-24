using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    [Collection("BulkData")]
    public class DeleteEdgeCaseTests
    {
        private readonly HttpClient _client;

        public DeleteEdgeCaseTests(BulkDataFixture fixture)
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
        public async Task PostDelete_MiddleOfThreeRecords_OnlyRemovesTargetRowAsync()
        {
            // Arrange
            var names = new[] { "DeleteTest_A", "DeleteTest_B", "DeleteTest_C" };
            var ids = new List<string>();

            foreach (var name in names)
            {
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Name", name),
                    new KeyValuePair<string, string>("_save_action", "continue"),
                });
                var createResponse = await _client.PostAsync("/admin/Ingredient/add/", formData);
                Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
                ids.Add(ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient"));
            }

            // Act
            var deleteResponse = await _client.PostAsync(
                $"/admin/Ingredient/{ids[1]}/delete/",
                new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

            // Verify A still exists
            var responseA = await _client.GetAsync($"/admin/Ingredient/{ids[0]}/change/");
            var htmlA = await responseA.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
            Assert.Contains("DeleteTest_A", htmlA);

            // Verify B was deleted (fetching it returns 404)
            var responseB = await _client.GetAsync($"/admin/Ingredient/{ids[1]}/change/");
            Assert.Equal(HttpStatusCode.NotFound, responseB.StatusCode);

            // Verify C still exists
            var responseC = await _client.GetAsync($"/admin/Ingredient/{ids[2]}/change/");
            var htmlC = await responseC.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, responseC.StatusCode);
            Assert.Contains("DeleteTest_C", htmlC);
        }

        [Fact]
        public async Task PostDelete_AlreadyDeletedRecord_Returns404Async()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "DeleteTwice"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", formData);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            var firstDelete = await _client.PostAsync(
                $"/admin/Ingredient/{id}/delete/",
                new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));
            Assert.Equal(HttpStatusCode.Redirect, firstDelete.StatusCode);

            // Act
            var secondDelete = await _client.PostAsync(
                $"/admin/Ingredient/{id}/delete/",
                new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
        }

        [Fact]
        public async Task PostDelete_NonExistentId_Returns404Async()
        {
            // Arrange / Act
            var response = await _client.PostAsync(
                "/admin/Ingredient/999999/delete/",
                new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task PostDelete_Ingredient_DoesNotAffectCategoryCountAsync()
        {
            // Arrange — capture category count before
            var catHtmlBefore = await (await _client.GetAsync("/admin/Category/")).Content.ReadAsStringAsync();
            var countMatch = Regex.Match(catHtmlBefore, @"(\d+)\s+categor");
            Assert.True(countMatch.Success, "Expected category count in list page");
            var countBefore = int.Parse(countMatch.Groups[1].Value);

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "CrossEntityTest"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", formData);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            // Act
            await _client.PostAsync(
                $"/admin/Ingredient/{id}/delete/",
                new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()));

            // Assert
            var catHtmlAfter = await (await _client.GetAsync("/admin/Category/")).Content.ReadAsStringAsync();
            var countMatchAfter = Regex.Match(catHtmlAfter, @"(\d+)\s+categor");
            Assert.True(countMatchAfter.Success);
            var countAfter = int.Parse(countMatchAfter.Groups[1].Value);
            Assert.Equal(countBefore, countAfter);
        }

        [Fact]
        public async Task GetDeleteConfirmation_ExistingRecord_ShowsRecordDetailsAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "ConfirmDetailIngredient"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", formData);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            // Act
            var response = await _client.GetAsync($"/admin/Ingredient/{id}/delete/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ConfirmDetailIngredient", html);
        }
    }
}
