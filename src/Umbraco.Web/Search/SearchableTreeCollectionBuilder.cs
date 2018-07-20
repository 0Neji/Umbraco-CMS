﻿using Umbraco.Core.Composing;
using Umbraco.Core.Services;
using Umbraco.Web.Trees;

namespace Umbraco.Web.Search
{
    internal class SearchableTreeCollectionBuilder : LazyCollectionBuilderBase<SearchableTreeCollectionBuilder, SearchableTreeCollection, ISearchableTree>
    {
        private readonly IApplicationTreeService _treeService;

        public SearchableTreeCollectionBuilder(IContainer container, IApplicationTreeService treeService)
            : base(container)
        {
            _treeService = treeService;
        }

        protected override SearchableTreeCollectionBuilder This => this;

        public override SearchableTreeCollection CreateCollection()
        {
            return new SearchableTreeCollection(CreateItems(), _treeService);
        }
    }
}
