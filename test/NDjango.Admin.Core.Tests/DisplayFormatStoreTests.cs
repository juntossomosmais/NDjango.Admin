using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace NDjango.Admin.Core.Tests
{
    public class DisplayFormatStoreTests
    {

        private readonly DisplayFormatStore _target;

        public DisplayFormatStoreTests()
        {
            _target = new DisplayFormatStore();
        }

        [Fact]
        public void Constructor_should_create_with_dict()
        {
            // Arrange
            var dict = new Dictionary<DataType, List<DisplayFormatDescriptor>>
            {
                [DataType.Date] = new List<DisplayFormatDescriptor>
                {
                    new DisplayFormatDescriptor("DefaultFormat", "{0:G}"){ IsDefault = true},
                    new DisplayFormatDescriptor("Format1", "{0:F}"),
                    new DisplayFormatDescriptor("Format2", "{0:f}")
                },
                [DataType.Int32] = new List<DisplayFormatDescriptor>
                {
                    new DisplayFormatDescriptor("DefaultFormat", "{0:D}") { IsDefault = true }
                }
            };

            // Act
            var target = new DisplayFormatStore(dict);

            // Assert
            foreach (var (type, formats) in dict)
            {
                foreach (var format in formats)
                {
                    Assert.True(target.TryGetFormat(type, format.Name, out var expectedFormat));
                    Assert.Same(format, expectedFormat);
                }
            }
        }

        [Fact]
        public void Clear_should_clear()
        {
            // Arrange
            _target.AddOrUpdate(DataType.Bool, "test", "{0:S0|1}", true);
            Assert.True(_target.Any());

            // Act
            _target.Clear();

            // Assert
            Assert.False(_target.Any());
        }

        [Theory]
        [InlineData(DataType.String, "test", "{0:${0}}", true)]
        [InlineData(DataType.String, "test", "{0:$${0}}", false)]
        public void AddOrUpdate_should_add_or_update_format(DataType type, string name, string format, bool isDefault)
        {
            // Arrange & Act
            var formatDesc = _target.AddOrUpdate(type, name, format, isDefault);

            // Assert
            Assert.True(_target.Any());
            Assert.Equal(isDefault, formatDesc.IsDefault);
            Assert.Equal(name, formatDesc.Name);
            Assert.Equal(format, formatDesc.Format);
        }

        [Fact]
        public void SetAttrDisplayFormat_should_throw_error_on_wrong_formats()
        {
            var meta = new MetaData();
            var attr = meta.CreateEntityAttr(new MetaEntityAttrDescriptor { Parent = meta.EntityRoot, DataType = DataType.String });

            //the following assignments must be processed correctly
            attr.DisplayFormat = "{0:d}";
            attr.DisplayFormat = "Total: {0:C2} грн";
            attr.DisplayFormat = "{0:yyyy-MM-dd}";

            //the following must fail
            Assert.Throws<InvalidDataFormatException>(() => attr.DisplayFormat = "{0:d");
            Assert.Throws<InvalidDataFormatException>(() => attr.DisplayFormat = "{0n:}");
            Assert.Throws<InvalidDataFormatException>(() => attr.DisplayFormat = "{1:F}");
        }

        [Fact]
        public void SetDefault_ExistingType_SetsCorrectFormat()
        {
            // Arrange
            _target.AddOrUpdate(DataType.Int32, "Format1", "{0:D5}");
            _target.AddOrUpdate(DataType.Int32, "Format2", "{0:D10}");

            // Act
            _target.SetDefault(DataType.Int32, "Format2");

            // Assert
            _target.TryGetFormat(DataType.Int32, "Format1", out var f1);
            Assert.False(f1.IsDefault);
            _target.TryGetFormat(DataType.Int32, "Format2", out var f2);
            Assert.True(f2.IsDefault);
        }

        [Fact]
        public void SetDefault_NonExistingType_DoesNotThrow()
        {
            // Arrange & Act & Assert
            _target.SetDefault(DataType.Guid, "Missing");
        }

        [Fact]
        public void GetDefault_ExistingType_ReturnsDefaultFormat()
        {
            // Arrange
            _target.AddOrUpdate(DataType.Date, "Short", "{0:d}");
            _target.AddOrUpdate(DataType.Date, "Long", "{0:D}", true);

            // Act
            var result = _target.GetDefault(DataType.Date);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Long", result.Name);
        }

        [Fact]
        public void GetDefault_NoDefaultSet_ReturnsNull()
        {
            // Arrange
            _target.AddOrUpdate(DataType.Date, "Short", "{0:d}");

            // Act
            var result = _target.GetDefault(DataType.Date);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetDefault_NonExistingType_ReturnsNull()
        {
            // Arrange & Act
            var result = _target.GetDefault(DataType.Guid);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Delete_ExistingFormat_RemovesIt()
        {
            // Arrange
            _target.AddOrUpdate(DataType.String, "test", "{0}");

            // Act
            _target.Delete(DataType.String, "test");

            // Assert
            Assert.False(_target.TryGetFormat(DataType.String, "test", out _));
        }

        [Fact]
        public void Delete_NonExistingType_DoesNotThrow()
        {
            // Arrange & Act & Assert
            _target.Delete(DataType.Guid, "Missing");
        }

        [Fact]
        public void TryGetFormat_NonExistingType_ReturnsFalse()
        {
            // Arrange & Act
            var result = _target.TryGetFormat(DataType.Guid, "Any", out var desc);

            // Assert
            Assert.False(result);
            Assert.Null(desc);
        }

        [Fact]
        public void AddOrUpdate_IsDefault_ClearsOtherDefaults()
        {
            // Arrange
            _target.AddOrUpdate(DataType.Bool, "First", "{0:S0|1}", true);

            // Act
            _target.AddOrUpdate(DataType.Bool, "Second", "{0:SNo|Yes}", true);

            // Assert
            _target.TryGetFormat(DataType.Bool, "First", out var first);
            Assert.False(first.IsDefault);
            _target.TryGetFormat(DataType.Bool, "Second", out var second);
            Assert.True(second.IsDefault);
        }

        [Fact]
        public void AddOrUpdate_UpdateExisting_UpdatesFormatAndIsDefault()
        {
            // Arrange
            _target.AddOrUpdate(DataType.Int32, "test", "{0:D5}", false);

            // Act
            _target.AddOrUpdate(DataType.Int32, "test", "{0:D10}", true);

            // Assert
            _target.TryGetFormat(DataType.Int32, "test", out var desc);
            Assert.Equal("{0:D10}", desc.Format);
            Assert.True(desc.IsDefault);
        }

        [Fact]
        public void GetEnumerator_NonGeneric_Works()
        {
            // Arrange
            _target.AddOrUpdate(DataType.Int32, "test", "{0:D5}");

            // Act
            var enumerator = ((System.Collections.IEnumerable)_target).GetEnumerator();

            // Assert
            Assert.True(enumerator.MoveNext());
        }
    }
}
