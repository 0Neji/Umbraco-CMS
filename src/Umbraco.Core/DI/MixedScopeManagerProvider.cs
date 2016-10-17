﻿using LightInject;
using LightInject.Web;

namespace Umbraco.Core.DI
{
    // by default, the container's scope manager provider is PerThreadScopeManagerProvider,
    // and then container.EnablePerWebRequestScope() replaces it with PerWebRequestScopeManagerProvider,
    // however if any delate has been compiled already at that point, it captures the scope
    // manager provider and changing it afterwards has no effect for that delegate.
    //
    // therefore, Umbraco uses the mixed scope manager provider, which initially wraps an instance
    // of PerThreadScopeManagerProvider and then can replace that wrapped instance with an instance
    // of PerWebRequestScopeManagerProvider - but all delegates see is the mixed one - and therefore
    // they can transition without issues.
    //
    // the mixed provider is installed in container.ConfigureUmbracoCore() and then,
    // when doing eg container.EnableMvc() or anything that does container.EnablePerWebRequestScope()
    // we need to take great care to preserve the mixed scope manager provider!

    public class MixedScopeManagerProvider : IScopeManagerProvider
    {
        private IScopeManagerProvider _provider;

        public MixedScopeManagerProvider()
        {
            _provider = new PerThreadScopeManagerProvider();
        }

        public void EnablePerWebRequestScope()
        {
            if (_provider is PerWebRequestScopeManagerProvider) return;
            _provider = new PerWebRequestScopeManagerProvider();
        }

        public ScopeManager GetScopeManager()
        {
            return _provider.GetScopeManager();
        }
    }
}

