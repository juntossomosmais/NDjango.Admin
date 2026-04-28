using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

using NDjango.Admin.MongoDB;

using Xunit;

namespace NDjango.Admin.MongoDB.Tests
{
    #region Test document classes

    public class SimpleDocument
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class DocumentWithBsonId
    {
        [BsonId]
        public string CustomKey { get; set; }
        public string Title { get; set; }
    }

    public class DocumentWithBsonIgnore
    {
        public ObjectId Id { get; set; }
        public string Visible { get; set; }
        [BsonIgnore]
        public string Ignored { get; set; }
    }

    public class DocumentWithBsonElement
    {
        public ObjectId Id { get; set; }
        [BsonElement("custom_name")]
        public string CustomField { get; set; }
        public string RegularField { get; set; }
    }

    public class DocumentWithCollections
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public List<string> Tags { get; set; }
        public int[] Scores { get; set; }
    }

    public class DocumentWithGuid
    {
        public Guid Id { get; set; }
        public string Label { get; set; }
    }

    public enum DocumentStatus
    {
        Active = 0,
        Inactive = 1,
        Archived = 2
    }

    public class DocumentWithEnum
    {
        public ObjectId Id { get; set; }
        public DocumentStatus Status { get; set; }
    }

    public class DocumentWithAllTypes
    {
        public ObjectId Id { get; set; }
        public string StringProp { get; set; }
        public int IntProp { get; set; }
        public long LongProp { get; set; }
        public decimal DecimalProp { get; set; }
        public bool BoolProp { get; set; }
        public DateTime DateTimeProp { get; set; }
        public double DoubleProp { get; set; }
        public float FloatProp { get; set; }
        public short ShortProp { get; set; }
        public byte ByteProp { get; set; }
        public Guid GuidProp { get; set; }
    }

    public class DocumentWithDisplayAttribute
    {
        public ObjectId Id { get; set; }
        [Display(Name = "Full Name", Description = "The full name of the person")]
        public string Name { get; set; }
    }

    public class DocumentWithNullableObjectId
    {
        public ObjectId? Id { get; set; }
        public string Value { get; set; }
    }

    public class NestedObject
    {
        public string Street { get; set; }
        public string City { get; set; }
    }

    public class DocumentWithComplexType
    {
        public ObjectId Id { get; set; }
        public NestedObject Address { get; set; }
    }

    public class DocumentWithConventionId
    {
        public string Id { get; set; }
        public string Data { get; set; }
    }

    public class DocumentWithNullableEnum
    {
        public ObjectId Id { get; set; }
        public DocumentStatus? Status { get; set; }
    }

    public class DocumentWithSearchFields : IAdminSettings<DocumentWithSearchFields>
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Internal { get; set; }
        public PropertyList<DocumentWithSearchFields> SearchFields =>
            new(x => x.Name, x => x.Description);
    }

    public class DocumentWithTimestamps
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class DocumentWithNullableUpdatedAt
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class DocumentWithDateTimeOffsetCreatedAt
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class DocumentWithStringCreatedAt
    {
        public ObjectId Id { get; set; }
        public string CreatedAt { get; set; }
    }

    public class DocumentWithNonTimestampDateTime
    {
        public ObjectId Id { get; set; }
        public DateTime SomeOtherDate { get; set; }
    }

    public class DocumentWithCreatedDate
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ValidationDocument
    {
        public ObjectId Id { get; set; }

        [MaxLength(50)]
        public string MaxLengthString { get; set; }

        [StringLength(maximumLength: 100, MinimumLength = 3)]
        public string StringLengthField { get; set; }

        [MinLength(5)]
        public string MinLengthField { get; set; }

        [MinLength(0)]
        public string ZeroMinLengthField { get; set; }

        [Range(1, 1000)]
        public int IntRange { get; set; }

        [Range(typeof(DateTime), "2020-01-01", "2030-12-31")]
        public DateTime DateRange { get; set; }

        [RegularExpression(@"^\d{5}$", ErrorMessage = "Must be 5 digits")]
        public string PostalCode { get; set; }

        [RegularExpression(@"(?i)^hello$")]
        public string CaseInsensitivePattern { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Url]
        public string Website { get; set; }

        [Phone]
        public string Phone { get; set; }
    }

    #endregion

    public class MongoMetaDataLoaderTests
    {
        [Fact]
        public void SimpleDocument_ProducesCorrectMetaEntity()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "simple_docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            Assert.Equal("SimpleDocument", entity.Id);
            Assert.Equal("Simple Document", entity.Name);
            Assert.Equal(typeof(SimpleDocument), entity.ClrType);
            Assert.True(entity.IsEditable);
            Assert.Equal("simple_docs", entity.DbSetName);
        }

        [Fact]
        public void ObjectIdProperty_IsMappedAsStringDataType()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var idAttr = entity.Attributes.First(a => a.PropName == "Id");

            Assert.Equal(DataType.String, idAttr.DataType);
            Assert.True(idAttr.IsPrimaryKey);
        }

        [Fact]
        public void BsonIgnore_PropertiesAreSkipped()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithBsonIgnore), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            Assert.DoesNotContain(entity.Attributes, a => a.PropName == "Ignored");
            Assert.Contains(entity.Attributes, a => a.PropName == "Visible");
        }

        [Fact]
        public void BsonId_OnNonIdProperty_IsDetectedAsPrimaryKey()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithBsonId), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var pkAttr = entity.Attributes.First(a => a.PropName == "CustomKey");

            Assert.True(pkAttr.IsPrimaryKey);
            Assert.Equal(DataType.String, pkAttr.DataType);
        }

        [Fact]
        public void BsonElement_SetsExprCorrectly()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithBsonElement), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var customAttr = entity.Attributes.First(a => a.PropName == "CustomField");
            var regularAttr = entity.Attributes.First(a => a.PropName == "RegularField");

            Assert.Equal("custom_name", customAttr.Expr);
            Assert.Equal("RegularField", regularAttr.Expr);
        }

        [Fact]
        public void CollectionTypes_AreIncludedAsStringDataType()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithCollections), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var tagsAttr = entity.Attributes.First(a => a.PropName == "Tags");
            var scoresAttr = entity.Attributes.First(a => a.PropName == "Scores");

            Assert.Equal(DataType.String, tagsAttr.DataType);
            Assert.Equal(DataType.String, scoresAttr.DataType);
        }

        [Fact]
        public void DefaultCollection_HasIsEditableTrue()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            Assert.True(entity.IsEditable);
        }

        [Fact]
        public void ReadOnlyCollection_HasIsEditableFalse()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs", isReadOnly: true)
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            Assert.False(entity.IsEditable);
        }

        [Fact]
        public void ReadOnlyCollection_AllAttributes_HaveIsEditableFalse()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs", isReadOnly: true)
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            foreach (var attr in entity.Attributes)
            {
                Assert.False(attr.IsEditable);
            }
        }

        [Fact]
        public void RegularFields_AreEditable_PkIsNotEditable()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var pkAttr = entity.Attributes.First(a => a.IsPrimaryKey);
            Assert.False(pkAttr.IsEditable);

            var regularAttrs = entity.Attributes.Where(a => !a.IsPrimaryKey).ToList();
            foreach (var attr in regularAttrs)
            {
                Assert.True(attr.IsEditable);
            }
        }

        [Fact]
        public void PrimaryKey_HasShowOnCreateFalse_RegularFields_HaveShowOnCreateTrue()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var pkAttr = entity.Attributes.First(a => a.IsPrimaryKey);
            Assert.False(pkAttr.ShowOnCreate);
            Assert.True(pkAttr.ShowOnEdit);
            Assert.False(pkAttr.IsEditable);

            var regularAttrs = entity.Attributes.Where(a => !a.IsPrimaryKey).ToList();
            foreach (var attr in regularAttrs)
            {
                Assert.True(attr.ShowOnCreate);
                Assert.True(attr.ShowOnEdit);
                Assert.True(attr.IsEditable);
            }
        }

        [Fact]
        public void HidePrimaryKeys_HidesPkFromView()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = true };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var idAttr = entity.Attributes.First(a => a.IsPrimaryKey);
            Assert.False(idAttr.ShowOnView);
        }

        [Fact]
        public void HidePrimaryKeys_False_ShowsPkInView()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var idAttr = entity.Attributes.First(a => a.IsPrimaryKey);
            Assert.True(idAttr.ShowOnView);
        }

        [Fact]
        public void PropertyFilters_ExcludeMatchingProperties()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };
            options.AddPropertyFilter(p => p.Name != "Age");

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            Assert.DoesNotContain(entity.Attributes, a => a.PropName == "Age");
            Assert.Contains(entity.Attributes, a => a.PropName == "Name");
        }

        [Fact]
        public void EntityFilters_ExcludeMatchingEntities()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs"),
                new MongoCollectionDescriptor(typeof(DocumentWithGuid), "guids")
            };
            var options = new MongoMetaDataLoaderOptions();
            options.AddEntityFilter(t => t != typeof(SimpleDocument));

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            Assert.Single(model.EntityRoot.SubEntities);
            Assert.Equal(typeof(DocumentWithGuid), model.EntityRoot.SubEntities[0].ClrType);
        }

        [Fact]
        public void GuidProperty_IsSupportedAsGuidDataType()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithGuid), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var idAttr = entity.Attributes.First(a => a.PropName == "Id");

            Assert.Equal(DataType.Guid, idAttr.DataType);
            Assert.True(idAttr.IsPrimaryKey);
        }

        [Fact]
        public void EnumProperty_IsSupported()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithEnum), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var statusAttr = entity.Attributes.First(a => a.PropName == "Status");

            Assert.NotNull(statusAttr.DefaultEditor);
            Assert.Contains("Active", statusAttr.DisplayFormat);
        }

        [Fact]
        public void NullableEnum_IsSupported()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithNullableEnum), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var statusAttr = entity.Attributes.First(a => a.PropName == "Status");

            Assert.NotNull(statusAttr.DefaultEditor);
        }

        [Fact]
        public void AllNumericAndCommonTypes_AreMapped()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithAllTypes), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();

            Assert.Equal(DataType.String, entity.Attributes.First(a => a.PropName == "Id").DataType);
            Assert.Equal(DataType.String, entity.Attributes.First(a => a.PropName == "StringProp").DataType);
            Assert.Equal(DataType.Int32, entity.Attributes.First(a => a.PropName == "IntProp").DataType);
            Assert.Equal(DataType.Int64, entity.Attributes.First(a => a.PropName == "LongProp").DataType);
            Assert.Equal(DataType.Currency, entity.Attributes.First(a => a.PropName == "DecimalProp").DataType);
            Assert.Equal(DataType.Bool, entity.Attributes.First(a => a.PropName == "BoolProp").DataType);
            Assert.Equal(DataType.DateTime, entity.Attributes.First(a => a.PropName == "DateTimeProp").DataType);
            Assert.Equal(DataType.Float, entity.Attributes.First(a => a.PropName == "DoubleProp").DataType);
            Assert.Equal(DataType.Float, entity.Attributes.First(a => a.PropName == "FloatProp").DataType);
            Assert.Equal(DataType.Word, entity.Attributes.First(a => a.PropName == "ShortProp").DataType);
            Assert.Equal(DataType.Byte, entity.Attributes.First(a => a.PropName == "ByteProp").DataType);
            Assert.Equal(DataType.Guid, entity.Attributes.First(a => a.PropName == "GuidProp").DataType);
        }

        [Fact]
        public void DisplayAttribute_SetsCaption()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithDisplayAttribute), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var nameAttr = entity.Attributes.First(a => a.PropName == "Name");

            Assert.Equal("Full Name", nameAttr.Caption);
            Assert.Equal("The full name of the person", nameAttr.Description);
        }

        [Fact]
        public void NullableObjectId_IsMappedAsString()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithNullableObjectId), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var idAttr = entity.Attributes.First(a => a.PropName == "Id");

            Assert.Equal(DataType.String, idAttr.DataType);
            Assert.True(idAttr.IsPrimaryKey);
        }

        [Fact]
        public void ComplexType_IsMappedAsStringDataType()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithComplexType), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var addressAttr = entity.Attributes.First(a => a.PropName == "Address");

            Assert.Equal(DataType.String, addressAttr.DataType);
        }

        [Fact]
        public void ConventionId_PropertyNamedId_IsDetectedAsPk()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithConventionId), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var idAttr = entity.Attributes.First(a => a.PropName == "Id");

            Assert.True(idAttr.IsPrimaryKey);
        }

        [Fact]
        public void MultipleCollections_AllAreLoaded()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "simple"),
                new MongoCollectionDescriptor(typeof(DocumentWithGuid), "guids")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            Assert.Equal(2, model.EntityRoot.SubEntities.Count);
        }

        [Fact]
        public void EntityNamePlural_IsSetCorrectly()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            Assert.Equal("Simple Documents", entity.NamePlural);
        }

        [Fact]
        public void IAdminSettings_SearchFields_AreDetected()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithSearchFields), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            Assert.NotNull(entity.SearchFields);
            Assert.Equal(2, entity.SearchFields.Count);
            Assert.Contains("Name", entity.SearchFields);
            Assert.Contains("Description", entity.SearchFields);
        }

        [Fact]
        public void NonVisibleAttributes_HaveShowOnViewTrue()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var nonPkAttrs = entity.Attributes.Where(a => !a.IsPrimaryKey);
            foreach (var attr in nonPkAttrs)
            {
                Assert.True(attr.ShowOnView);
            }
        }

        [Fact]
        public void AttributeId_ComposedCorrectly()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "docs")
            };
            var options = new MongoMetaDataLoaderOptions { HidePrimaryKeys = false };

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            var entity = model.EntityRoot.SubEntities.Single();
            var nameAttr = entity.Attributes.First(a => a.PropName == "Name");

            Assert.Equal("SimpleDocument.Name", nameAttr.Id);
        }

        [Fact]
        public void SkipOption_ExcludesDocumentType()
        {
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(SimpleDocument), "simple"),
                new MongoCollectionDescriptor(typeof(DocumentWithGuid), "guids")
            };
            var options = new MongoMetaDataLoaderOptions();
            options.Skip<SimpleDocument>();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            Assert.Single(model.EntityRoot.SubEntities);
            Assert.Equal(typeof(DocumentWithGuid), model.EntityRoot.SubEntities[0].ClrType);
        }

        [Fact]
        public void AutoTimestamp_CreatedAt_IsNotEditable()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithTimestamps), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var createdAtAttr = entity.Attributes.First(a => a.PropName == "CreatedAt");
            Assert.False(createdAtAttr.IsEditable);
            Assert.False(createdAtAttr.ShowOnCreate);
            Assert.True(createdAtAttr.ShowOnEdit);
        }

        [Fact]
        public void AutoTimestamp_UpdatedAt_IsNotEditable()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithTimestamps), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var updatedAtAttr = entity.Attributes.First(a => a.PropName == "UpdatedAt");
            Assert.False(updatedAtAttr.IsEditable);
            Assert.False(updatedAtAttr.ShowOnCreate);
            Assert.True(updatedAtAttr.ShowOnEdit);
        }

        [Fact]
        public void AutoTimestamp_RegularFieldsRemainEditable()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithTimestamps), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var nameAttr = entity.Attributes.First(a => a.PropName == "Name");
            Assert.True(nameAttr.IsEditable);
            Assert.True(nameAttr.ShowOnCreate);
            Assert.True(nameAttr.ShowOnEdit);
        }

        [Fact]
        public void CollectionType_IsNotEditable()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithCollections), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var tagsAttr = entity.Attributes.First(a => a.PropName == "Tags");
            Assert.False(tagsAttr.IsEditable);
            Assert.False(tagsAttr.ShowOnCreate);
            Assert.True(tagsAttr.ShowOnEdit);
        }

        [Fact]
        public void ComplexType_IsNotEditable()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithComplexType), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var addressAttr = entity.Attributes.First(a => a.PropName == "Address");
            Assert.False(addressAttr.IsEditable);
            Assert.False(addressAttr.ShowOnCreate);
            Assert.True(addressAttr.ShowOnEdit);
        }

        [Fact]
        public void AutoTimestamp_NullableDateTime_IsDetectedAsAutoTimestamp()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithNullableUpdatedAt), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var updatedAtAttr = entity.Attributes.First(a => a.PropName == "UpdatedAt");
            Assert.False(updatedAtAttr.IsEditable);
            Assert.False(updatedAtAttr.ShowOnCreate);
            Assert.True(updatedAtAttr.ShowOnEdit);
        }

        [Fact]
        public void AutoTimestamp_DateTimeOffset_IsDetectedAsAutoTimestamp()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithDateTimeOffsetCreatedAt), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var createdAtAttr = entity.Attributes.First(a => a.PropName == "CreatedAt");
            Assert.False(createdAtAttr.IsEditable);
            Assert.False(createdAtAttr.ShowOnCreate);
            Assert.True(createdAtAttr.ShowOnEdit);
        }

        [Fact]
        public void AutoTimestamp_StringCreatedAt_IsNotDetectedAsAutoTimestamp()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithStringCreatedAt), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var createdAtAttr = entity.Attributes.First(a => a.PropName == "CreatedAt");
            Assert.True(createdAtAttr.IsEditable);
            Assert.True(createdAtAttr.ShowOnCreate);
            Assert.True(createdAtAttr.ShowOnEdit);
        }

        [Fact]
        public void AutoTimestamp_DateTimeWithNonTimestampName_IsNotDetectedAsAutoTimestamp()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithNonTimestampDateTime), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var dateAttr = entity.Attributes.First(a => a.PropName == "SomeOtherDate");
            Assert.True(dateAttr.IsEditable);
            Assert.True(dateAttr.ShowOnCreate);
            Assert.True(dateAttr.ShowOnEdit);
        }

        [Fact]
        public void AutoTimestamp_CreatedDate_IsDetectedAsAutoTimestamp()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(DocumentWithCreatedDate), "docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            // Act
            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            // Assert
            var entity = model.EntityRoot.SubEntities.Single();
            var createdDateAttr = entity.Attributes.First(a => a.PropName == "CreatedDate");
            Assert.False(createdDateAttr.IsEditable);
            Assert.False(createdDateAttr.ShowOnCreate);
            Assert.True(createdDateAttr.ShowOnEdit);
        }
    }

    public class MongoValidationMetadataTests
    {
        private readonly MetaEntity _entity;

        public MongoValidationMetadataTests()
        {
            // Arrange
            var model = new MetaData();
            var collections = new List<MongoCollectionDescriptor>
            {
                new MongoCollectionDescriptor(typeof(ValidationDocument), "validation_docs")
            };
            var options = new MongoMetaDataLoaderOptions();

            var loader = new MongoMetaDataLoader(model, collections, options);
            loader.LoadFromCollections();

            _entity = model.EntityRoot.SubEntities.Single();
        }

        private MetaEntityAttr FindAttr(string propName)
        {
            return _entity.Attributes.FirstOrDefault(a => a.PropName == propName);
        }

        [Fact]
        public void LoadFromCollections_MaxLengthAttribute_PopulatesMaxLength()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.MaxLengthString));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(50, attr.MaxLength);
        }

        [Fact]
        public void LoadFromCollections_StringLengthAttribute_PopulatesBothBounds()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.StringLengthField));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(100, attr.MaxLength);
            Assert.Equal(3, attr.MinLength);
        }

        [Fact]
        public void LoadFromCollections_MinLengthAttribute_PopulatesMinLength()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.MinLengthField));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(5, attr.MinLength);
        }

        [Fact]
        public void LoadFromCollections_MinLengthZero_DoesNotPopulateMinLength()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.ZeroMinLengthField));

            // Assert
            Assert.NotNull(attr);
            Assert.Null(attr.MinLength);
        }

        [Fact]
        public void LoadFromCollections_IntRangeAttribute_PopulatesMinMaxValue()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.IntRange));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(1m, attr.MinValue);
            Assert.Equal(1000m, attr.MaxValue);
            Assert.Null(attr.MinDateTime);
            Assert.Null(attr.MaxDateTime);
        }

        [Fact]
        public void LoadFromCollections_DateTimeRange_PopulatesMinMaxDateTime()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.DateRange));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(new DateTime(2020, 1, 1), attr.MinDateTime);
            Assert.Equal(new DateTime(2030, 12, 31), attr.MaxDateTime);
            Assert.Null(attr.MinValue);
            Assert.Null(attr.MaxValue);
        }

        [Fact]
        public void LoadFromCollections_RegularExpression_PopulatesPatternAndErrorMessage()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.PostalCode));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(@"^\d{5}$", attr.RegexPattern);
            Assert.Equal("Must be 5 digits", attr.RegexErrorMessage);
        }

        [Fact]
        public void LoadFromCollections_RegularExpressionWithInlineFlags_StillStoresPattern()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.CaseInsensitivePattern));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal("(?i)^hello$", attr.RegexPattern);
        }

        [Fact]
        public void LoadFromCollections_EmailAddress_SetsEmailInputType()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.Email));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(InputTypeHint.Email, attr.InputType);
            Assert.Null(attr.RegexPattern);
        }

        [Fact]
        public void LoadFromCollections_Url_SetsUrlInputType()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.Website));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(InputTypeHint.Url, attr.InputType);
        }

        [Fact]
        public void LoadFromCollections_Phone_SetsTelInputTypeWithoutRegex()
        {
            // Arrange
            // Metadata is loaded once in the constructor.

            // Act
            var attr = FindAttr(nameof(ValidationDocument.Phone));

            // Assert
            Assert.NotNull(attr);
            Assert.Equal(InputTypeHint.Tel, attr.InputType);
            Assert.Null(attr.RegexPattern);
        }
    }
}
