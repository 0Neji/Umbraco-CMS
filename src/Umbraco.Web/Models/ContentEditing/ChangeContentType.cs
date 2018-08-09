﻿using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Umbraco.Web.Models.ContentEditing
{
    public class ChangeContentType
    {
        public int ContentNodeId { get; set; }

        public int NewContentTypeId { get; set; }

        public int NewTemplateId { get; set; }

        public IEnumerable<FieldMap> FieldMap { get; set; }
    }

    public class FieldMap
    {
        public string FromAlias { get; set; }

        public string ToAlias { get; set; }
    }

    public class FieldMapValue
    {
        public string ToAlias { get; set; }

        public object CurrentValue { get; set; }
    }

    public class AvailableContentTypes
    {
        public string CurrentNodeName { get; set; }

        public IContentType CurrentContentType { get; set; }

        public IEnumerable<IContentType> ContentTypes { get; set; }
    }
}
