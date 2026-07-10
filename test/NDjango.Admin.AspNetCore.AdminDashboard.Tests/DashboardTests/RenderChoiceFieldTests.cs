using System.Collections.Generic;
using System.Text;

using NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers;
using NDjango.Admin.AspNetCore.AdminDashboard.ViewModels;

using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.DashboardTests
{
    // Unit-level coverage for the enum/constant-list dropdown rendering, independent of a database.
    public class RenderChoiceFieldTests
    {
        private static FieldViewModel BuildField(bool required, object value = null) => new FieldViewModel
        {
            PropName = "Color",
            DataType = DataType.String,
            IsRequired = required,
            IsEditable = true,
            Value = value,
            Choices = new List<FieldChoice>
            {
                new FieldChoice { Id = "0", Text = "Red" },
                new FieldChoice { Id = "1", Text = "Green" },
                new FieldChoice { Id = "2", Text = "Blue" },
            },
        };

        [Fact]
        public void RenderChoiceField_RequiredField_RendersSelectWithOptionsAndNoEmptyChoice()
        {
            // Arrange
            var content = new StringBuilder();
            var field = BuildField(required: true);

            // Act
            ViewRenderer.RenderChoiceField(content, field);
            var html = content.ToString();

            // Assert
            Assert.Contains("<select id=\"id_Color\" name=\"Color\" required>", html);
            Assert.Contains("<option value=\"0\">Red</option>", html);
            Assert.Contains("<option value=\"2\">Blue</option>", html);
            Assert.DoesNotContain("<option value=\"\">", html);
        }

        [Fact]
        public void RenderChoiceField_OptionalField_RendersEmptyPlaceholderOption()
        {
            // Arrange
            var content = new StringBuilder();
            var field = BuildField(required: false);

            // Act
            ViewRenderer.RenderChoiceField(content, field);
            var html = content.ToString();

            // Assert
            Assert.Contains("<select id=\"id_Color\" name=\"Color\">", html);
            Assert.Contains("<option value=\"\">---------</option>", html);
        }

        [Fact]
        public void RenderChoiceField_ValueMatchesText_PreSelectsByEnumName()
        {
            // Arrange
            // A freshly loaded record exposes the enum value, whose ToString() is the name (matches Text).
            var content = new StringBuilder();
            var field = BuildField(required: true, value: "Green");

            // Act
            ViewRenderer.RenderChoiceField(content, field);
            var html = content.ToString();

            // Assert
            Assert.Contains("<option value=\"1\" selected>Green</option>", html);
            Assert.DoesNotContain("<option value=\"0\" selected>", html);
        }

        [Fact]
        public void RenderChoiceField_ValueMatchesId_PreSelectsBySubmittedValue()
        {
            // Arrange
            // A form re-rendered after a validation error exposes the submitted option value (matches Id).
            var content = new StringBuilder();
            var field = BuildField(required: true, value: "2");

            // Act
            ViewRenderer.RenderChoiceField(content, field);
            var html = content.ToString();

            // Assert
            Assert.Contains("<option value=\"2\" selected>Blue</option>", html);
        }
    }
}
