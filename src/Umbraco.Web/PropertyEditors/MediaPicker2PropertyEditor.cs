﻿using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// Represents a media picker property editor.
    /// </summary>
    [DataEditor(Constants.PropertyEditors.Aliases.MediaPicker2, EditorType.PropertyValue | EditorType.MacroParameter, "mediapicker", ValueTypes.Text, Group = "media", Icon = "icon-picture")]
    public class MediaPicker2PropertyEditor : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaPicker2PropertyEditor"/> class.
        /// </summary>
        public MediaPicker2PropertyEditor(ILogger logger)
            : base(logger)
        { }

        /// <inheritdoc />
        protected override IConfigurationEditor CreateConfigurationEditor() => new MediaPickerConfigurationEditor();
    }
}
