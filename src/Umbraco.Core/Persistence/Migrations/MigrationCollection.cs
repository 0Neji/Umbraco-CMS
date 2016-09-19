﻿using System.Collections.Generic;
using Umbraco.Core.DependencyInjection;

namespace Umbraco.Core.Persistence.Migrations
{
    public class MigrationCollection : BuilderCollectionBase<IMigration>
    {
        public MigrationCollection(IEnumerable<IMigration> items) 
            : base(items)
        { }
    }
}
