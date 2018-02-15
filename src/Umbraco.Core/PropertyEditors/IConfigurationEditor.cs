﻿using System.Collections.Generic;

namespace Umbraco.Core.PropertyEditors
{
    /// <summary>
    /// Represents an editor for editing the configuration of editors.
    /// </summary>
    public interface IConfigurationEditor
    {
        /// <summary>
        /// Gets the fields.
        /// </summary>
        List<ConfigurationField> Fields { get; }

        /// <summary>
        /// Gets the default configuration.
        /// </summary>
        IDictionary<string, object> DefaultConfiguration { get; }

        /// <summary>
        /// Determines whether a configuration object is of the type expected by the configuration editor.
        /// </summary>
        bool IsConfiguration(object obj);

        // notes
        // ToConfigurationEditor returns a dictionary, and FromConfigurationEditor accepts a dictionary.
        // this is due to the way our front-end editors work, see DataTypeController.PostSave
        // and DataTypeConfigurationFieldDisplayResolver - we are not going to change it now.

        /// <summary>
        /// Converts the serialized database value into the actual configuration object.
        /// </summary>
        /// <remarks>Converting the configuration object to the serialized database value is
        /// achieved by simply serializing the configuration.</remarks>
        object FromDatabase(string configurationJson);

        /// <summary>
        /// Converts the values posted by the configuration editor into the actual configuration object.
        /// </summary>
        /// <param name="editorValues">The values posted by the configuration editor.</param>
        /// <param name="configuration">The current configuration object.</param>
        object FromConfigurationEditor(Dictionary<string, object> editorValues, object configuration);

        /// <summary>
        /// Converts the configuration object to values for the configuration editor.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        Dictionary<string, object> ToConfigurationEditor(object configuration);

        /// <summary>
        /// Converts the configuration object to values for the value editror.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        Dictionary<string, object> ToValueEditor(object configuration);
    }
}