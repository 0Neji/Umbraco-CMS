﻿using System.Collections.Generic;
using Umbraco.Core.ObjectResolution;
using CoreCurrent = Umbraco.Core.DependencyInjection.Current;
using WebCurrent = Umbraco.Web.Current;

// ReSharper disable once CheckNamespace
namespace Umbraco.Web.Mvc
{
    public class FilteredControllerFactoriesResolver : ManyObjectsResolverBase<FilteredControllerFactoryCollectionBuilder, FilteredControllerFactoryCollection, IFilteredControllerFactory>
    {
        public FilteredControllerFactoriesResolver(FilteredControllerFactoryCollectionBuilder builder) 
            : base(builder)
        { }

        public static FilteredControllerFactoriesResolver Current { get; }
            = new FilteredControllerFactoriesResolver(CoreCurrent.Container.GetInstance<FilteredControllerFactoryCollectionBuilder>());

        public IEnumerable<IFilteredControllerFactory> Factories => WebCurrent.FilteredControllerFactories;
    }
}
