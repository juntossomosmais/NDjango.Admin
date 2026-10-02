using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using NDjango.Admin.AspNetCore.AdminDashboard.ViewModels;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.RelationshipTests
{
    /// <summary>
    /// A foreign key shows the label of the record it points at instead of its raw key, as Django
    /// shows <c>str(obj.fk)</c>: in the changelist, in the lookup popup, and next to the raw id
    /// input of the form (Django's <c>raw_id_fields</c>). The label is the related entity's
    /// <c>ShowInLookup</c> attributes — for the fixture's Category and Restaurant, their Name.
    /// </summary>
    public class ForeignKeyLabelTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ForeignKeyLabelTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        private async Task<string> GetHtmlAsync(string url)
        {
            var response = await _client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        [Fact]
        public async Task List_ForeignKeyColumn_ShowsTheRelatedRecordLabelAsync()
        {
            // Arrange — Bella Roma points at Italian, Sakura at Japanese.
            const string url = "/admin/Restaurant/";

            // Act
            var html = await GetHtmlAsync(url);

            // Assert
            Assert.Contains(">Italian</td>", html);
            Assert.Contains(">Japanese</td>", html);
        }

        [Fact]
        public async Task List_ForeignKeyColumn_UsesTheRelationshipCaptionAsync()
        {
            // Arrange
            const string url = "/admin/Restaurant/";

            // Act
            var html = await GetHtmlAsync(url);

            // Assert
            Assert.Contains(">Category</a></th>", html);
            Assert.DoesNotContain(">Category Id</a></th>", html);
        }

        [Fact]
        public async Task List_ForeignKeyColumn_KeepsTheRawKeyOnHoverAndSortsByItAsync()
        {
            // Arrange
            const string url = "/admin/Restaurant/";

            // Act
            var html = await GetHtmlAsync(url);

            // Assert
            Assert.Contains("title=\"CategoryId: ", html);
            Assert.Contains("?sort=CategoryId", html);
        }

        [Fact]
        public async Task List_AnotherEntity_ShowsItsOwnForeignKeyLabelsAsync()
        {
            // Arrange — Margherita Pizza belongs to Bella Roma, Sushi Roll to Sakura.
            const string url = "/admin/MenuItem/";

            // Act
            var html = await GetHtmlAsync(url);

            // Assert
            Assert.Contains(">Bella Roma</td>", html);
            Assert.Contains(">Sakura</td>", html);
            Assert.Contains(">Restaurant</a></th>", html);
        }

        [Fact]
        public async Task Popup_ForeignKeyColumns_ShowLabelsAndRowsCarryTheirOwnLabelAsync()
        {
            // Arrange — the Category popup hands the picked category's label back to the form.
            const string categoryPopup = "/admin/Category/?_to_field=id&_popup=1";
            const string restaurantPopup = "/admin/Restaurant/?_to_field=id&_popup=1";

            // Act
            var categories = await GetHtmlAsync(categoryPopup);
            var restaurants = await GetHtmlAsync(restaurantPopup);

            // Assert
            Assert.Contains("data-label=\"Italian\"", categories);
            Assert.Contains("data-label=\"Bella Roma\"", restaurants);
            Assert.Contains(">Italian</td>", restaurants);
        }

        [Fact]
        public async Task EditForm_ForeignKeyField_ShowsTheLabelNextToTheKeyAsync()
        {
            // Arrange — restaurant 1 (Bella Roma) points at category Italian.
            const string url = "/admin/Restaurant/1/change/";

            // Act
            var html = await GetHtmlAsync(url);

            // Assert
            Assert.Contains("<strong class=\"related-label\" id=\"label_id_CategoryId\">Italian</strong>", html);
        }

        [Fact]
        public async Task AddForm_ForeignKeyField_RendersAnEmptyLabelForThePopupToFillAsync()
        {
            // Arrange
            const string url = "/admin/Restaurant/add/";

            // Act
            var html = await GetHtmlAsync(url);

            // Assert
            Assert.Contains("<strong class=\"related-label\" id=\"label_id_CategoryId\"></strong>", html);
        }

        [Fact]
        public async Task AddForm_AfterAValidationError_ShowsTheLabelOfTheSubmittedKeyAsync()
        {
            // Arrange — Name is required; the submitted CategoryId (2 = Japanese) survives the re-render.
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", ""),
                new KeyValuePair<string, string>("Address", "Somewhere"),
                new KeyValuePair<string, string>("CategoryId", "2"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Restaurant/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("id=\"label_id_CategoryId\">Japanese</strong>", html);
        }

        [Fact]
        public void CellText_WithoutALabelForTheKey_FallsBackToTheRawValue()
        {
            // Arrange — the referenced record has no label (or is gone): the cell keeps the key.
            var column = new ColumnViewModel
            {
                PropName = "CategoryId",
                LookupLabels = new Dictionary<string, string> { ["1"] = "Italian" }
            };
            var plainColumn = new ColumnViewModel { PropName = "Name" };

            // Act
            var labeled = ViewRenderer.CellText(column, 1);
            var unlabeled = ViewRenderer.CellText(column, 42);
            var empty = ViewRenderer.CellText(column, null);
            var plain = ViewRenderer.CellText(plainColumn, "Bella Roma");

            // Assert
            Assert.Equal("Italian", labeled);
            Assert.Equal("42", unlabeled);
            Assert.Equal("", empty);
            Assert.Equal("Bella Roma", plain);
        }
    }
}
