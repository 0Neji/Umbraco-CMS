﻿using Newtonsoft.Json.Linq;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// Represents the configuration for the gid value editor.
    /// </summary>
    public class Grid2Configuration
    {
        [ConfigurationField("items", "Grid", "views/propertyeditors/grid2/grid.prevalues.html", Description = "Grid configuration")]
        public JObject Items { get; set; }

        [ConfigurationField("rte", "Rich text editor", "views/propertyeditors/rte/rte.prevalues.html", Description = "Rich text editor configuration")]
        public JObject Rte { get; set; }
    }
}
