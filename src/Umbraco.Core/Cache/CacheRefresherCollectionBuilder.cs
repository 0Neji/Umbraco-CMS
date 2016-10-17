﻿using System.Collections.Generic;
using LightInject;
using Umbraco.Core.DI;

namespace Umbraco.Core.Cache
{
    public class CacheRefresherCollectionBuilder : LazyCollectionBuilderBase<CacheRefresherCollectionBuilder, CacheRefresherCollection, ICacheRefresher>
    {
        public CacheRefresherCollectionBuilder(IServiceContainer container)
            : base(container)
        { }

        protected override CacheRefresherCollectionBuilder This => this;
    }
}
