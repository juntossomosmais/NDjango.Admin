using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class RazorViewDispatcherMutationTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public RazorViewDispatcherMutationTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        // === List View: Sort Direction Default (kills id=671) ===

        [Fact]
        public async Task ListPage_SortByNameWithoutDirParam_ShowsAscendingArrowAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/?sort=Name");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — default sort direction should be "asc", rendering ascending arrow
            // Mutation id=671 changes "asc" default to "" which would show descending arrow
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("&#9650;", html);
        }

        // === List View: Delete Action AllowEmptySelection (kills id=742) ===

        [Fact]
        public async Task ListPage_DeleteSelectedAction_DisallowsEmptySelectionAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — delete_selected action specifically must have data-allow-empty="false"
            // Mutation id=742 flips false → true on the built-in delete action
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("value=\"delete_selected\" data-allow-empty=\"false\"", html);
        }

        // === Edit Form: FK Value Populated (kills id=781, id=782) ===

        [Fact]
        public async Task EditForm_ForeignKeyField_ShowsCurrentValueAsync()
        {
            // Arrange — Restaurant 1 (Bella Roma) has CategoryId=1

            // Act
            var response = await _client.GetAsync("/admin/Restaurant/1/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — FK input must show the current value
            // Mutation id=781 inverts null check, id=782 removes the value-setting block
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("name=\"CategoryId\" value=\"1\"", html);
        }

        // === Create Form: No Readonly-Value Fields (kills id=785, id=788) ===

        [Fact]
        public async Task CreateForm_Category_DoesNotShowReadonlyValueFieldsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — value-generated fields (Id) should be hidden on create, not readonly
            // Mutation id=785 uses ShowOnEdit instead of ShowOnCreate (shows Id)
            // Mutation id=788 removes the continue (shows Id)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("readonly-value", html);
        }

        [Fact]
        public async Task CreateForm_Restaurant_DoesNotShowReadonlyValueFieldsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — same check for entity with FK relationships
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("readonly-value", html);
        }

        // === Create Form: Required Indicator (kills id=790) ===

        [Fact]
        public async Task CreateForm_RequiredField_RendersRequiredStarAndAttributeAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — Name is [Required], should show * and required attribute
            // Mutation id=790 flips !attr.IsNullable → attr.IsNullable, removing required
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<span class=\"required\">*</span>", html);
            Assert.Matches("name=\"Name\"[^>]*required", html);
        }

        // === Sidebar Groups Content (kills id=851) ===

        [Fact]
        public async Task DeletePage_Sidebar_ContainsEntityPluralNamesAsync()
        {
            // Arrange & Act — delete page only shows entity names in sidebar
            var response = await _client.GetAsync("/admin/Ingredient/1/delete/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — sidebar EntityGroupItems must have populated NamePlural
            // Mutation id=851 empties the EntityGroupItem initializer
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("sidebar-model-item", html);
            // "Restaurants" only appears in sidebar, not in Ingredient delete content
            Assert.Contains("Restaurants", html);
        }

        [Fact]
        public async Task EditForm_Sidebar_ContainsEntityLinksAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/1/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — sidebar links should point to correct entity URLs
            // Mutation id=851 nullifies EntityId, producing broken links like "/admin//"
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("sidebar-model-item", html);
            // Verify actual entity links in sidebar (not just class presence)
            Assert.Contains("/admin/Ingredient/", html);
        }

        // === Delete Page: Record Values (kills id=816) ===

        [Fact]
        public async Task DeletePage_Restaurant_ShowsDataFieldsOnly_NotNavigationPropertiesAsync()
        {
            // Arrange & Act — Restaurant has FK to Category (Lookup attr)
            var response = await _client.GetAsync("/admin/Restaurant/1/delete/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — delete summary shows data fields but NOT navigation properties
            // Mutation id=816 changes && to || which includes Lookup attrs like Category
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Bella Roma", html);
            Assert.Contains("123 Main St", html);
            Assert.Contains("delete-summary", html);
            // Navigation property "Category" should NOT appear as a record value label
            // (it would appear as <strong>Category:</strong> if the mutation is active)
            Assert.DoesNotContain("<strong>Category:</strong>", html);
        }

        // === Bulk Delete Page: Record Values (kills id=842) ===

        [Fact]
        public async Task BulkDeletePage_Restaurant_ShowsDataFieldsOnly_NotNavigationPropertiesAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Restaurant/action/delete/?ids=1&ids=2");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — bulk delete should show data fields but NOT Lookup/navigation properties
            // Mutation id=842 changes && to || which includes Lookup attrs
            // Bulk delete renders as: "Restaurant: Id: 1, Name: Bella Roma, ..."
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Bella Roma", html);
            Assert.Contains("Sakura", html);
            Assert.Contains("delete-summary", html);
            // Navigation property "Category" should NOT appear as a field in record display
            // With mutation, it would add ", Category: " to the display string
            Assert.DoesNotContain(", Category: </li>", html);
        }

        // === NoCoverage: Flash Message Default Level (kills id=736) ===

        [Fact]
        public async Task ListPage_FlashMessage_WithoutLevel_DefaultsToSuccessAsync()
        {
            // Arrange & Act — _msg without _msg_level
            var response = await _client.GetAsync("/admin/Category/?_msg=TestFlash");
            var html = await response.Content.ReadAsStringAsync();

            // Assert — MessageLevel defaults to "success" when not specified
            // Mutation id=736 changes "success" → "" which renders class=""
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("TestFlash", html);
            Assert.Contains("class=\"success\"", html);
        }

        // === NoCoverage: Non-Existent Entity → 404 ===

        [Fact]
        public async Task CreateForm_NonExistentEntity_Returns404Async()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/NonExistent/add/");

            // Assert — kills NoCoverage id=752
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task EditForm_NonExistentEntity_Returns404Async()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/NonExistent/1/change/");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeletePage_NonExistentEntity_Returns404Async()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/NonExistent/1/delete/");

            // Assert — kills NoCoverage id=807
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task BulkDeletePage_NonExistentEntity_Returns404Async()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/NonExistent/action/delete/?ids=1");

            // Assert — kills NoCoverage id=826
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // === NoCoverage: Bulk Delete Empty IDs → Redirect ===

        [Fact]
        public async Task BulkDeletePage_EmptyIds_RedirectsToEntityListAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Category/action/delete/");

            // Assert — kills NoCoverage id=831/832/833
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/Category/", response.Headers.Location.ToString());
        }
    }
}
