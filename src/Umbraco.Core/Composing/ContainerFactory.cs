﻿using System;
using System.Configuration;
using System.Reflection;

namespace Umbraco.Core.Composing
{
    /// <summary>
    /// Creates the container.
    /// </summary>
    public static class ContainerFactory
    {
        // cannot use typeof().AssemblyQualifiedName on the web container - we don't reference it
        // a normal Umbraco site should run on the web container, but an app may run on the core one
        private const string CoreLightInjectContainerTypeName = "Umbraco.Core.Composing.LightInject.LightInjectContainer,Umbraco.Core";
        private const string WebLightInjectContainerTypeName = "Umbraco.Web.Composing.LightInject.LightInjectContainer,Umbraco.Web";

        /// <summary>
        /// Creates a new instance of the configured container.
        /// </summary>
        /// <remarks>
        /// To override the default LightInjectContainer, add an appSetting named umbracoContainerType with
        /// a fully qualified type name to a class with a static method "Create" returning an IContainer.
        /// </remarks>
        public static IContainer Create()
        {
            Type type;

            var configuredTypeName = ConfigurationManager.AppSettings["umbracoContainerType"];
            if (configuredTypeName.IsNullOrWhiteSpace())
            {
                // try to get the web LightInject container type,
                // else the core LightInject container type
                type = Type.GetType(configuredTypeName = WebLightInjectContainerTypeName) ??
                       Type.GetType(configuredTypeName = CoreLightInjectContainerTypeName);
            }
            else
            {
                // try to get the configured container type
                type = Type.GetType(configuredTypeName);
            }

            if (type == null)
                throw new Exception($"Cannot find container factory class '{configuredTypeName}'.");

            var factoryMethod = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            if (factoryMethod == null)
                throw new Exception($"Container factory class '{configuredTypeName}' does not have a public static method named Create.");

            var container = factoryMethod.Invoke(null, Array.Empty<object>()) as IContainer;
            if (container == null)
                throw new Exception($"Container factory '{configuredTypeName}' did not return an IContainer implementation.");

            // self-register the container - this is where it should happen
            // but - we do NOT want to do it!
            //container.RegisterInstance(container);

            return container;
        }
    }
}
