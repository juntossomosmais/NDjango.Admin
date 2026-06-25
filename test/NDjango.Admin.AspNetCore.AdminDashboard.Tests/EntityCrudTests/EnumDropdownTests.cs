using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    // Enum fields must render as a <select> dropdown on the add/edit form, both for plain enums
    // (option ids = underlying int values) and enums mapped to a string via a value converter
    // (option ids = provider string values).
    public class EnumDropdownTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public EnumDropdownTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task CreateForm_PlainEnum_RendersSelectWithUnderlyingValuesAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/EnumWidget/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<select id=\"id_Color\" name=\"Color\"", html);
            Assert.Contains("<option value=\"0\">Red</option>", html);
            Assert.Contains("<option value=\"1\">Green</option>", html);
            Assert.Contains("<option value=\"2\">Blue</option>", html);
        }

        [Fact]
        public async Task CreateForm_EnumViaValueConverter_RendersSelectWithProviderValuesAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/EnumWidget/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<select id=\"id_Size\" name=\"Size\"", html);
            Assert.Contains("<option value=\"small\">Small</option>", html);
            Assert.Contains("<option value=\"medium\">Medium</option>", html);
            Assert.Contains("<option value=\"large\">Large</option>", html);
        }

        [Fact]
        public async Task CreateForm_EnumField_DoesNotRenderPlainTextInputAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/EnumWidget/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.DoesNotContain("<input type=\"text\" id=\"id_Color\"", html);
            Assert.DoesNotContain("<input type=\"text\" id=\"id_Size\"", html);
        }
    }
}
