using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityListTests
{
    public class ActionTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ActionTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task ListPage_RendersActionBar_WithDeleteAndCustomActionsAsync()
        {
            // Arrange
            // Category has IAdminSettings with custom actions defined

            // Act
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("select name=\"action\"", html);
            Assert.Contains("delete_selected", html);
            Assert.Contains("Delete selected categories", html);
            Assert.Contains("test_action", html);
            Assert.Contains("Test action for categories", html);
            Assert.Contains("test_error_action", html);
            Assert.Contains("Test error action", html);
        }

        [Fact]
        public async Task ListPage_RendersCheckboxes_OnEachRowAsync()
        {
            // Arrange
            // Category list has 3 seeded records

            // Act
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("id=\"action-toggle\"", html);
            Assert.Contains("name=\"_selected_ids\"", html);
            Assert.Contains("class=\"action-select\"", html);
        }

        [Fact]
        public async Task ListPage_InPopupMode_DoesNotRenderActionBarAsync()
        {
            // Arrange
            // Popup mode should suppress the action bar

            // Act
            var response = await _client.GetAsync("/admin/Category/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("id=\"changelist-form\"", html);
            Assert.DoesNotContain("select name=\"action\"", html);
            Assert.DoesNotContain("action-toggle", html);
        }

        [Fact]
        public async Task ListPage_ShowsFlashMessage_WhenMsgQueryParamPresentAsync()
        {
            // Arrange
            var url = "/admin/Category/?_msg=Operation+successful&_msg_level=success";

            // Act
            var response = await _client.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("messagelist", html);
            Assert.Contains("Operation successful", html);
            Assert.Contains("class=\"success\"", html);
        }

        [Fact]
        public async Task ActionPost_WithCustomAction_ExecutesAndRedirectsWithMessageAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "test_action"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
                new KeyValuePair<string, string>("_selected_ids", "2"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("/admin/Category/", location);
            Assert.Contains("_msg=", location);
            Assert.Contains("_msg_level=success", location);
        }

        [Fact]
        public async Task ActionPost_WithDeleteSelected_RedirectsToBulkDeleteConfirmationAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "delete_selected"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
                new KeyValuePair<string, string>("_selected_ids", "2"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("/admin/Category/action/delete/", location);
            Assert.Contains("ids=1", location);
            Assert.Contains("ids=2", location);
        }

        [Fact]
        public async Task BulkDeleteConfirmation_ShowsSelectedRecordsAsync()
        {
            // Arrange
            // Categories with Id 1 and 2 are seeded ("Italian" and "Japanese")

            // Act
            var response = await _client.GetAsync("/admin/Category/action/delete/?ids=1&ids=2");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Are you sure", html);
            Assert.Contains("Italian", html);
            Assert.Contains("Japanese", html);
            Assert.Contains("2 categories", html);
            Assert.Contains("action/delete/", html);
            Assert.Contains("Yes, I", html);
            Assert.Contains("No, take me back", html);
        }

        [Fact]
        public async Task BulkDeletePost_DeletesRecordsAndRedirectsWithMessageAsync()
        {
            // Arrange
            // First create records to delete so we don't affect other tests
            var createForm1 = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "BulkDeleteTest1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            var createForm2 = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "BulkDeleteTest2"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            await _client.PostAsync("/admin/Ingredient/add/", createForm1);
            await _client.PostAsync("/admin/Ingredient/add/", createForm2);

            // Find the new IDs by listing
            var listResponse = await _client.GetAsync("/admin/Ingredient/");
            var listHtml = await listResponse.Content.ReadAsStringAsync();
            Assert.Contains("BulkDeleteTest1", listHtml);
            Assert.Contains("BulkDeleteTest2", listHtml);

            // Extract IDs from the change links - use the ingredient names we created
            // The seeded ingredients are id 1,2,3. Our new ones will be 4,5 or higher.
            // We'll use ingredients we created for deletion test
            // Let's find the ids from checkboxes
            var idList = new List<string>();
            var searchIdx = 0;
            while (true)
            {
                var marker = "name=\"_selected_ids\" value=\"";
                var pos = listHtml.IndexOf(marker, searchIdx);
                if (pos < 0)
                    break;
                var start = pos + marker.Length;
                var end = listHtml.IndexOf("\"", start);
                idList.Add(listHtml.Substring(start, end - start));
                searchIdx = end;
            }

            // Find IDs of the BulkDeleteTest items - they should be the last two
            // We'll delete the last two IDs which correspond to our newly created items
            Assert.True(idList.Count >= 2, "Expected at least 2 ingredients to be listed");
            var deleteId1 = idList[idList.Count - 2];
            var deleteId2 = idList[idList.Count - 1];

            var deleteForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("_selected_ids", deleteId1),
                new KeyValuePair<string, string>("_selected_ids", deleteId2),
            });

            // Act
            var response = await _client.PostAsync("/admin/Ingredient/action/delete/", deleteForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("/admin/Ingredient/", location);
            Assert.Contains("_msg=", location);
            Assert.Contains("deleted", location);
            Assert.Contains("_msg_level=success", location);
        }

        [Fact]
        public async Task ActionPost_WithErrorAction_ShowsErrorMessageAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "test_error_action"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("_msg_level=error", location);
        }

        [Fact]
        public async Task ListPage_ShowsErrorFlashMessage_WhenErrorLevelPresentAsync()
        {
            // Arrange
            var url = "/admin/Category/?_msg=Something+went+wrong&_msg_level=error";

            // Act
            var response = await _client.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("messagelist", html);
            Assert.Contains("Something went wrong", html);
            Assert.Contains("class=\"error\"", html);
        }
    }
}
