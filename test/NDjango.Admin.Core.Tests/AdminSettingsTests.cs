using System;
using System.Collections;
using System.Linq;

using Xunit;

namespace NDjango.Admin.Core.Tests
{
    public class AdminSettingsTests
    {
        private class SampleDto
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public SampleDto Inner { get; set; }
        }

        private class TestSettings : IAdminSettings<TestSettings> { }

        [Fact]
        public void Constructor_WithValidPropertySelectors_ExtractsPropertyNames()
        {
            // Arrange & Act
            var list = new PropertyList<SampleDto>(x => x.Name, x => x.Age);

            // Assert
            Assert.Equal(2, list.Count);
            Assert.Equal("Name", list[0]);
            Assert.Equal("Age", list[1]);
        }

        [Fact]
        public void Constructor_WithAPathThroughARelationship_ExtractsTheDottedPath()
        {
            // Arrange & Act: Django's search_fields = ["inner__name"]. A join table is identified
            // by what it points at, so without this the only searchable thing on one is a foreign
            // key — and finding a row means pasting an id looked up on another screen.
            var list = new PropertyList<SampleDto>(x => x.Inner.Name);

            // Assert
            Assert.Equal("Inner.Name", Assert.Single(list));
        }

        [Fact]
        public void Constructor_WithADeeperPath_ExtractsEverySegment()
        {
            // Arrange & Act: two relationships in. The depth the search runs at is derived from
            // the longest path declared, so this is the shape that decides it.
            var list = new PropertyList<SampleDto>(x => x.Inner.Inner.Name);

            // Assert
            Assert.Equal("Inner.Inner.Name", Assert.Single(list));
        }

        [Fact]
        public void Constructor_WithAPathAndAFlatProperty_KeepsBothAndTheirOrder()
        {
            // Arrange & Act
            var list = new PropertyList<SampleDto>(x => x.Name, x => x.Inner.Name);

            // Assert
            Assert.Equal(2, list.Count);
            Assert.Equal("Name", list[0]);
            Assert.Equal("Inner.Name", list[1]);
        }

        [Fact]
        public void Constructor_WithNoSelectors_CreatesEmptyList()
        {
            // Arrange & Act
            var list = new PropertyList<SampleDto>();

            // Assert
            Assert.Empty(list);
        }

        [Fact]
        public void Constructor_WithUnaryExpression_ExtractsPropertyName()
        {
            // Arrange & Act
            // int property causes a Convert (boxing) UnaryExpression wrapping a MemberExpression
            var list = new PropertyList<SampleDto>(x => x.Age);

            // Assert
            var single = Assert.Single(list);
            Assert.Equal("Age", single);
        }

        [Fact]
        public void Validate_InvalidExpression_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            // A method call expression is neither MemberExpression nor UnaryExpression wrapping MemberExpression
            var ex = Assert.Throws<ArgumentException>(
                () => new PropertyList<SampleDto>(x => x.Name.ToString()));

            Assert.Contains("Expression must be a property access", ex.Message);
        }

        [Fact]
        public void Validate_ExpressionNotRootedAtTheParameter_ThrowsArgumentException()
        {
            // Arrange: 🔴 this replaces Validate_NestedProperty_ThrowsArgumentException, which
            // asserted that x => x.Inner.Name was refused. That refusal is what this change
            // reverses — a path through a relationship is now the supported way to search a join
            // table — so the test is not relaxed, it is pointed at what is still refused.
            //
            // What remains invalid is a member access that does not start at the entity: a
            // captured variable reads a value from the closure and names no column to search.
            var captured = new SampleDto { Name = "outside the entity" };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () => new PropertyList<SampleDto>(x => captured.Name));

            Assert.Contains("Property must be reached from the entity by member access", ex.Message);
        }

        [Fact]
        public void Indexer_ReturnsCorrectName()
        {
            // Arrange
            var list = new PropertyList<SampleDto>(x => x.Name, x => x.Age);

            // Act
            var first = list[0];
            var second = list[1];

            // Assert
            Assert.Equal("Name", first);
            Assert.Equal("Age", second);
        }

        [Fact]
        public void GetEnumerator_NonGeneric_Works()
        {
            // Arrange
            var list = new PropertyList<SampleDto>(x => x.Name, x => x.Age);

            // Act
            var enumerable = (IEnumerable)list;
            var items = enumerable.Cast<string>().ToList();

            // Assert
            Assert.Equal(2, items.Count);
            Assert.Equal("Name", items[0]);
            Assert.Equal("Age", items[1]);
        }

        [Fact]
        public void IAdminSettings_DefaultSearchFields_ReturnsEmptyList()
        {
            // Arrange
            IAdminSettings<TestSettings> settings = new TestSettings();

            // Act
            var searchFields = settings.SearchFields;

            // Assert
            Assert.NotNull(searchFields);
            Assert.Empty(searchFields);
        }

        [Fact]
        public void IAdminSettings_DefaultActions_ReturnsNull()
        {
            // Arrange
            IAdminSettings<TestSettings> settings = new TestSettings();

            // Act
            var actions = settings.Actions;

            // Assert
            Assert.Null(actions);
        }
    }
}
