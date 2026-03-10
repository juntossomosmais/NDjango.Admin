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
    public class UpdateEdgeCaseTests
    {
        private readonly HttpClient _client;

        public UpdateEdgeCaseTests(BulkDataFixture fixture)
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
        public async Task PostUpdate_MiddleOfThreeRecords_OnlyModifiesTargetRowAsync()
        {
            // Arrange
            var names = new[] { "UpdateTest_A", "UpdateTest_B", "UpdateTest_C" };
            var ids = new List<string>();

            foreach (var name in names)
            {
                var formData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Name", name),
                    new KeyValuePair<string, string>("_save_action", "continue"),
                });
                var createResponse = await _client.PostAsync("/admin/Ingredient/add/", formData);
                ids.Add(ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient"));
            }

            // Act
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "UpdateTest_B_Modified"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var updateResponse = await _client.PostAsync($"/admin/Ingredient/{ids[1]}/change/", updateForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, updateResponse.StatusCode);

            var htmlA = await (await _client.GetAsync($"/admin/Ingredient/{ids[0]}/change/")).Content.ReadAsStringAsync();
            Assert.Contains("UpdateTest_A", htmlA);

            var htmlB = await (await _client.GetAsync($"/admin/Ingredient/{ids[1]}/change/")).Content.ReadAsStringAsync();
            Assert.Contains("UpdateTest_B_Modified", htmlB);

            var htmlC = await (await _client.GetAsync($"/admin/Ingredient/{ids[2]}/change/")).Content.ReadAsStringAsync();
            Assert.Contains("UpdateTest_C", htmlC);
        }

        [Fact]
        public async Task PostUpdate_NonExistentId_ThrowsAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Ghost"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(
                () => _client.PostAsync("/admin/Ingredient/999999/change/", formData));
        }

        [Fact]
        public async Task PostUpdate_OnlyNameSent_RecordStillAccessibleAsync()
        {
            // Arrange
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "PreserveFieldTest"),
                new KeyValuePair<string, string>("IsAllergen", "true"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", createForm);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            // Act
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "PreserveFieldTest_Renamed"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var updateResponse = await _client.PostAsync($"/admin/Ingredient/{id}/change/", updateForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, updateResponse.StatusCode);

            var editResponse = await _client.GetAsync($"/admin/Ingredient/{id}/change/");
            var html = await editResponse.Content.ReadAsStringAsync();
            Assert.Contains("PreserveFieldTest_Renamed", html);
        }

        [Fact]
        public async Task PostUpdate_RestaurantWithFk_DoesNotAffectOtherRestaurantsAsync()
        {
            // Arrange
            var rest1HtmlBefore = await (await _client.GetAsync("/admin/Restaurant/1/change/")).Content.ReadAsStringAsync();
            Assert.Contains("Bella Roma", rest1HtmlBefore);

            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "FkUpdateTarget"),
                new KeyValuePair<string, string>("Address", "789 Test St"),
                new KeyValuePair<string, string>("CategoryId", "1"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Restaurant/add/", createForm);
            var newId = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Restaurant");

            // Act
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "FkUpdateTarget_Modified"),
                new KeyValuePair<string, string>("Address", "789 Test St"),
                new KeyValuePair<string, string>("CategoryId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            await _client.PostAsync($"/admin/Restaurant/{newId}/change/", updateForm);

            // Assert
            var rest1HtmlAfter = await (await _client.GetAsync("/admin/Restaurant/1/change/")).Content.ReadAsStringAsync();
            Assert.Contains("Bella Roma", rest1HtmlAfter);
        }
    }
}
