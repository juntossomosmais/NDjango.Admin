using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NDjango.Admin.MongoDB
{
    /// <summary>
    /// Loads metadata from MongoDB collection descriptors using reflection.
    /// Mirrors <c>DbContextMetaDataLoader</c> but uses BSON attributes instead of EF metadata.
    /// </summary>
    public class MongoMetaDataLoader
    {
        private readonly MetaData _model;
        private readonly IReadOnlyList<MongoCollectionDescriptor> _collections;
        private readonly MongoMetaDataLoaderOptions _options;

        public MongoMetaDataLoader(MetaData model, IReadOnlyList<MongoCollectionDescriptor> collections, MongoMetaDataLoaderOptions options)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _collections = collections ?? throw new ArgumentNullException(nameof(collections));
            _options = options ?? new MongoMetaDataLoaderOptions();
        }

        /// <summary>
        /// Scans all registered collection descriptors and populates the MetaData model.
        /// </summary>
        public void LoadFromCollections()
        {
            foreach (var descriptor in _collections) {
                var documentType = descriptor.DocumentType;

                if (!ApplyEntityFilters(documentType))
                    continue;

                var entity = ProcessDocumentType(descriptor);
                if (entity != null) {
                    entity.Attributes.Reorder();
                    _model.EntityRoot.SubEntities.Add(entity);
                }
            }

            _model.EntityRoot.Attributes.Reorder();
            _model.EntityRoot.SubEntities.Reorder();
        }

        private bool ApplyEntityFilters(Type documentType)
        {
            foreach (var filter in _options.EntityFilters) {
                if (!filter.Invoke(documentType))
                    return false;
            }
            return true;
        }

        private bool ApplyPropertyFilters(PropertyInfo property)
        {
            foreach (var filter in _options.PropertyFilters) {
                if (!filter.Invoke(property))
                    return false;
            }
            return true;
        }

        private MetaEntity ProcessDocumentType(MongoCollectionDescriptor descriptor)
        {
            var documentType = descriptor.DocumentType;
            var typeName = documentType.Name;

            var entity = _model.CreateEntity();
            entity.Id = DataUtils.ComposeKey(null, typeName);
            entity.Name = DataUtils.PrettifyName(typeName);

            if (documentType.GetCustomAttribute(typeof(DisplayAttribute)) is DisplayAttribute displayAttr) {
                entity.Name = displayAttr.Name;
                entity.Description = displayAttr.Description;
            }

            entity.NamePlural = DataUtils.MakePlural(entity.Name);
            entity.ClrType = documentType;
            entity.IsEditable = !descriptor.IsReadOnly;
            entity.DbSetName = descriptor.CollectionName;

            // Check for IAdminSettings<T> interface for SearchFields
            var adminSettingsInterface = documentType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAdminSettings<>));

            if (adminSettingsInterface != null) {
                try {
                    var instance = Activator.CreateInstance(documentType);
                    var searchFieldsProp = adminSettingsInterface.GetProperty("SearchFields");
                    if (searchFieldsProp != null) {
                        entity.SearchFields = searchFieldsProp.GetValue(instance) as IReadOnlyList<string>;
                    }
                }
                catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or MemberAccessException) {
                    // Document can't be instantiated - leave SearchFields null
                }
            }

            var properties = documentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var attrCounter = 0;

            foreach (var property in properties) {
                if (!ApplyPropertyFilters(property))
                    continue;

                // Skip properties with [BsonIgnore]
                if (property.GetCustomAttribute<BsonIgnoreAttribute>() != null)
                    continue;

                var entityAttr = CreateEntityAttribute(entity, property, descriptor);
                if (entityAttr != null) {
                    if (entityAttr.Index == int.MaxValue) {
                        entityAttr.Index = attrCounter;
                    }
                    attrCounter++;
                    entity.Attributes.Add(entityAttr);
                }
            }

            return entity;
        }

        private MetaEntityAttr CreateEntityAttribute(MetaEntity entity, PropertyInfo property, MongoCollectionDescriptor descriptor)
        {
            var propertyType = property.PropertyType;
            var isPrimaryKey = IsPrimaryKeyProperty(property);

            // Determine data type
            DataType columnType;

            if (propertyType == typeof(ObjectId) || propertyType == typeof(ObjectId?)) {
                columnType = DataType.String;
            }
            else {
                // Unwrap Nullable<T> for enum detection since DataUtils does not handle Nullable<Enum>
                var unwrapped = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                columnType = unwrapped.IsEnum
                    ? DataUtils.GetDataTypeBySystemType(unwrapped.GetEnumUnderlyingType())
                    : DataUtils.GetDataTypeBySystemType(propertyType);

                if (columnType == DataType.Unknown) {
                    // Check if it's a collection type (excluding string and byte[])
                    if (IsCollectionType(propertyType)) {
                        columnType = DataType.String;
                    }
                    else if (IsComplexType(propertyType)) {
                        columnType = DataType.String;
                    }
                    else {
                        // Skip unknown types
                        return null;
                    }
                }
            }

            var entityAttr = _model.CreateEntityAttr(new MetaEntityAttrDescriptor()
            {
                Parent = entity
            });

            entityAttr.Id = DataUtils.ComposeKey(entity.Id, property.Name);

            // Expr: from [BsonElement("name")] or fall back to property name
            var bsonElementAttr = property.GetCustomAttribute<BsonElementAttribute>();
            entityAttr.Expr = bsonElementAttr?.ElementName ?? property.Name;

            // Caption: from [Display(Name="...")] or prettified name
            if (property.GetCustomAttribute(typeof(DisplayAttribute)) is DisplayAttribute displayAttr) {
                entityAttr.Caption = displayAttr.Name;
                entityAttr.Description = displayAttr.Description;
            }
            else {
                entityAttr.Caption = DataUtils.PrettifyName(property.Name);
            }

            entityAttr.DataType = columnType;
            entityAttr.PropInfo = property;
            entityAttr.PropName = property.Name;

            entityAttr.IsPrimaryKey = isPrimaryKey;
            entityAttr.IsNullable = IsNullableProperty(propertyType);

            entityAttr.ShowOnView = true;

            if (descriptor.IsReadOnly) {
                entityAttr.IsEditable = false;
                entityAttr.ShowOnCreate = false;
                entityAttr.ShowOnEdit = true;
            }
            else if (isPrimaryKey) {
                entityAttr.IsEditable = false;
                entityAttr.ShowOnCreate = false;
                entityAttr.ShowOnEdit = true;
            }
            else if (IsAutoTimestamp(property)) {
                entityAttr.IsEditable = false;
                entityAttr.ShowOnCreate = false;
                entityAttr.ShowOnEdit = true;
            }
            else if (IsCollectionType(propertyType) || IsComplexType(propertyType)) {
                entityAttr.IsEditable = false;
                entityAttr.ShowOnCreate = false;
                entityAttr.ShowOnEdit = true;
            }
            else {
                entityAttr.IsEditable = true;
                entityAttr.ShowOnCreate = true;
                entityAttr.ShowOnEdit = true;
            }

            // Hide primary keys from view if option is set
            if (_options.HidePrimaryKeys && isPrimaryKey) {
                entityAttr.ShowOnView = false;
            }

            // Run after the visibility defaults so the password auto-detection
            // (InputType + ShowOnView=false) is not clobbered by ShowOnView=true above.
            DataUtils.ApplyValidationAttributes(entityAttr, property);

            // Handle enum types
            var actualType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (actualType.IsEnum) {
                var veId = $"VE_{entity.Id}_{property.Name}";
                var editor = new ConstListValueEditor(veId);
                var fields = actualType.GetFields();
                foreach (var field in fields.Where(f => !f.Name.Equals("value__"))) {
                    editor.Values.Add(field.GetRawConstantValue().ToString(), field.Name);
                }
                entityAttr.DefaultEditor = editor;
                entityAttr.DisplayFormat = DataUtils.ComposeDisplayFormatForEnum(actualType);
            }

            return entityAttr;
        }

        private static bool IsPrimaryKeyProperty(PropertyInfo property)
        {
            // Explicit [BsonId] attribute
            if (property.GetCustomAttribute<BsonIdAttribute>() != null)
                return true;

            // Convention: property named "Id" or "_id"
            return string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(property.Name, "_id", StringComparison.Ordinal);
        }

        private static bool IsNullableProperty(Type type)
        {
            if (!type.IsValueType)
                return true;

            return Nullable.GetUnderlyingType(type) != null;
        }

        private static bool IsCollectionType(Type type)
        {
            if (type == typeof(string) || type == typeof(byte[]))
                return false;

            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static bool IsComplexType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying.IsPrimitive || underlying.IsEnum)
                return false;

            if (underlying == typeof(string) || underlying == typeof(decimal)
                || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)
                || underlying == typeof(TimeSpan) || underlying == typeof(Guid)
                || underlying == typeof(ObjectId)
                || underlying == typeof(DateOnly) || underlying == typeof(TimeOnly))
                return false;

            return underlying.IsClass || underlying.IsValueType;
        }

        private static bool IsAutoTimestamp(PropertyInfo property)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type != typeof(DateTime) && type != typeof(DateTimeOffset))
                return false;

            var name = property.Name;
            return name == "CreatedAt" || name == "UpdatedAt"
                || name == "CreatedDate" || name == "UpdatedDate"
                || name == "CreationDate" || name == "ModificationDate";
        }
    }
}
