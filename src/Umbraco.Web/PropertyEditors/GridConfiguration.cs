﻿using Umbraco.Core.PropertyEditors;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// Represents the configuration for the gid value editor.
    /// </summary>
    public class GridConfiguration
    {
        [ConfigurationField("items", "Grid", "views/propertyeditors/grid/grid.prevalues.html", Description = "Grid configuration")]
        public string Items { get; set; }

        [ConfigurationField("rte", "Rich text editor", "views/propertyeditors/rte/rte.prevalues.html", Description = "Rich text editor configuration")]
        public string Rte { get; set; }
    }
}