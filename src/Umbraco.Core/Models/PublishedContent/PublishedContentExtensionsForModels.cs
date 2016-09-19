﻿using System;
using Umbraco.Core.DependencyInjection;

namespace Umbraco.Core.Models.PublishedContent
{
    /// <summary>
    /// Provides strongly typed published content models services.
    /// </summary>
    internal static class PublishedContentExtensionsForModels
    {
        /// <summary>
        /// Creates a strongly typed published content model for an internal published content.
        /// </summary>
        /// <param name="content">The internal published content.</param>
        /// <returns>The strongly typed published content model.</returns>
        public static IPublishedContent CreateModel(this IPublishedContent content)
        {
            if (content == null)
                return null;

            // get model
            // if factory returns nothing, throw
            var model = Current.PublishedContentModelFactory.CreateModel(content);
            if (model == null)
                throw new Exception("IPublishedContentFactory returned null.");

            return model;
        }
    }
}
