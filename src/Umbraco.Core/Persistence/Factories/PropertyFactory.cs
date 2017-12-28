﻿using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Dtos;

namespace Umbraco.Core.Persistence.Factories
{
    internal static class PropertyFactory
    {
        public static IEnumerable<Property> BuildEntities(PropertyType[] propertyTypes, IReadOnlyCollection<PropertyDataDto> dtos, int publishedVersionId)
        {
            var properties = new List<Property>();
            var xdtos = dtos.GroupBy(x => x.PropertyTypeId).ToDictionary(x => x.Key, x => (IEnumerable<PropertyDataDto>) x);

            foreach (var propertyType in propertyTypes)
            {
                var property = propertyType.CreateProperty();

                try
                {
                    property.DisableChangeTracking();

                    // see notes in BuildDtos - we always have edit+published dtos

                    if (xdtos.TryGetValue(propertyType.Id, out var propDtos))
                    {
                        foreach (var propDto in propDtos)
                            property.FactorySetValue(propDto.LanguageId, propDto.Segment, propDto.VersionId == publishedVersionId, propDto.Value);
                    }

                    property.ResetDirtyProperties(false);
                    properties.Add(property);
                }
                finally
                {
                    property.EnableChangeTracking();
                }
            }

            return properties;
        }

        private static PropertyDataDto BuildDto(int versionId, Property property, int? nLanguageId, string segment, object value)
        {
            var dto = new PropertyDataDto { VersionId = versionId, PropertyTypeId = property.PropertyTypeId };

            if (nLanguageId.HasValue)
                dto.LanguageId = nLanguageId;

            if (segment != null)
                dto.Segment = segment;

            if (property.DataTypeDatabaseType == DataTypeDatabaseType.Integer)
            {
                if (value is bool || property.PropertyType.PropertyEditorAlias == Constants.PropertyEditors.TrueFalseAlias)
                {
                    dto.IntegerValue = value != null && string.IsNullOrEmpty(value.ToString()) ? 0 : Convert.ToInt32(value);
                }
                else if (value != null && string.IsNullOrWhiteSpace(value.ToString()) == false && int.TryParse(value.ToString(), out var val))
                {
                    dto.IntegerValue = val;
                }
            }
            else if (property.DataTypeDatabaseType == DataTypeDatabaseType.Decimal && value != null)
            {
                if (decimal.TryParse(value.ToString(), out var val))
                {
                    dto.DecimalValue = val; // property value should be normalized already
                }
            }
            else if (property.DataTypeDatabaseType == DataTypeDatabaseType.Date && value != null && string.IsNullOrWhiteSpace(value.ToString()) == false)
            {
                if (DateTime.TryParse(value.ToString(), out var date))
                {
                    dto.DateValue = date;
                }
            }
            else if (property.DataTypeDatabaseType == DataTypeDatabaseType.Ntext && value != null)
            {
                dto.TextValue = value.ToString();
            }
            else if (property.DataTypeDatabaseType == DataTypeDatabaseType.Nvarchar && value != null)
            {
                dto.VarcharValue = value.ToString();
            }

            return dto;
        }

        public static IEnumerable<PropertyDataDto> BuildDtos(int currentVersionId, int publishedVersionId, IEnumerable<Property> properties, out bool edited)
        {
            var propertyDataDtos = new List<PropertyDataDto>();
            edited = false;

            foreach (var property in properties)
            {
                if (property.PropertyType.IsPublishing)
                {
                    // publishing = deal with edit and published values
                    foreach (var propertyValue in property.Values)
                    {
                        // deal with published value
                        if (propertyValue.PublishedValue != null && publishedVersionId > 0)
                            propertyDataDtos.Add(BuildDto(publishedVersionId, property, propertyValue.LanguageId, propertyValue.Segment, propertyValue.PublishedValue));

                        // deal with edit value
                        if (propertyValue.EditedValue != null)
                            propertyDataDtos.Add(BuildDto(currentVersionId, property, propertyValue.LanguageId, propertyValue.Segment, propertyValue.EditedValue));

                        // deal with missing edit value (fix inconsistencies)
                        else if (propertyValue.PublishedValue != null)
                            propertyDataDtos.Add(BuildDto(currentVersionId, property, propertyValue.LanguageId, propertyValue.Segment, propertyValue.PublishedValue));

                        // use explicit equals here, else object comparison fails at comparing eg strings
                        var sameValues = propertyValue.PublishedValue == null ? propertyValue.EditedValue == null : propertyValue.PublishedValue.Equals(propertyValue.EditedValue);
                        edited |= !sameValues;
                    }
                }
                else
                {
                    foreach (var propertyValue in property.Values)
                    {
                        // not publishing = only deal with edit values
                        if (propertyValue.EditedValue != null)
                            propertyDataDtos.Add(BuildDto(currentVersionId, property, propertyValue.LanguageId, propertyValue.Segment, propertyValue.EditedValue));
                    }
                    edited = true;
                }
            }

            return propertyDataDtos;
        }
    }
}
