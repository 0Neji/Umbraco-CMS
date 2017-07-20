﻿using System;
using System.Collections.Generic;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;

namespace Umbraco.Web
{
    /// <summary>
    /// Class that encapsulates Umbraco information of a specific HTTP request
    /// </summary>
    public class UmbracoContext : DisposableObject, IDisposeOnRequestEnd
    {
        private readonly Lazy<IFacade> _facade;
        private string _previewToken;
        private bool? _previewing;

        #region Ensure Context

        /// <summary>
        /// Ensures that there is a "current" UmbracoContext.
        /// </summary>
        /// <param name="httpContext">An http context.</param>
        /// <param name="facadeService">A facade service.</param>
        /// <param name="webSecurity">A security helper.</param>
        /// <param name="umbracoSettings">The umbraco settings.</param>
        /// <param name="urlProviders">Some url providers.</param>
        /// <param name="replace">A value indicating whether to replace the existing context.</param>
        /// <returns>The "current" UmbracoContext.</returns>
        /// <remarks>
        /// fixme - this needs to be clarified
        ///
        /// If <paramref name="replace"/> is true then the "current" UmbracoContext is replaced
        /// with a new one even if there is one already. See <see cref="WebRuntimeComponent"/>. Has to do with
        /// creating a context at startup and not being able to access httpContext.Request at that time, so
        /// the OriginalRequestUrl remains unspecified until <see cref="UmbracoModule"/> replaces the context.
        ///
        /// This *has* to be done differently!
        ///
        /// See http://issues.umbraco.org/issue/U4-1890, http://issues.umbraco.org/issue/U4-1717
        ///
        /// </remarks>
        // used by
        // UmbracoModule BeginRequest (since it's a request it has an UmbracoContext)
        //   in BeginRequest so *late* ie *after* the HttpApplication has started (+ init? check!)
        // WebRuntimeComponent (and I'm not quite sure why)
        // -> because an UmbracoContext seems to be required by UrlProvider to get the "current" facade?
        //    note: at startup not sure we have an HttpContext.Current
        //          at startup not sure we have an httpContext.Request => hard to tell "current" url
        //          should we have a post-boot event of some sort for ppl that *need* ?!
        //          can we have issues w/ routing context?
        // and tests
        // can .ContentRequest be null? of course!
        public static UmbracoContext EnsureContext(
            IUmbracoContextAccessor umbracoContextAccessor,
            HttpContextBase httpContext,
            IFacadeService facadeService,
            WebSecurity webSecurity,
            IUmbracoSettingsSection umbracoSettings,
            IEnumerable<IUrlProvider> urlProviders,
            bool replace = false)
        {
            if (umbracoContextAccessor == null) throw new ArgumentNullException(nameof(umbracoContextAccessor));
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            if (facadeService == null) throw new ArgumentNullException(nameof(facadeService));
            if (webSecurity == null) throw new ArgumentNullException(nameof(webSecurity));
            if (umbracoSettings == null) throw new ArgumentNullException(nameof(umbracoSettings));
            if (urlProviders == null) throw new ArgumentNullException(nameof(urlProviders));

            // if there is already a current context, return if not replacing
            var current = umbracoContextAccessor.UmbracoContext;
            if (current != null && replace == false)
                return current;

            // create & assign to accessor, dispose existing if any
            umbracoContextAccessor.UmbracoContext?.Dispose();
            return umbracoContextAccessor.UmbracoContext = new UmbracoContext(httpContext, facadeService, webSecurity, umbracoSettings, urlProviders);
        }

        // initializes a new instance of the UmbracoContext class
        // internal for unit tests
        // otherwise it's used by EnsureContext above
        // warn: does *not* manage setting any IUmbracoContextAccessor
        internal UmbracoContext(
            HttpContextBase httpContext,
            IFacadeService facadeService,
            WebSecurity webSecurity,
            IUmbracoSettingsSection umbracoSettings,
            IEnumerable<IUrlProvider> urlProviders)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));
            if (facadeService == null) throw new ArgumentNullException(nameof(facadeService));
            if (webSecurity == null) throw new ArgumentNullException(nameof(webSecurity));
            if (umbracoSettings == null) throw new ArgumentNullException(nameof(umbracoSettings));
            if (urlProviders == null) throw new ArgumentNullException(nameof(urlProviders));

            // ensure that this instance is disposed when the request terminates, though we *also* ensure
            // this happens in the Umbraco module since the UmbracoCOntext is added to the HttpContext items.
            //
            // also, it *can* be returned by the container with a PerRequest lifetime, meaning that the
            // container *could* also try to dispose it.
            //
            // all in all, this context may be disposed more than once, but DisposableObject ensures that
            // it is ok and it will be actually disposed only once.
            httpContext.DisposeOnPipelineCompleted(this);

            ObjectCreated = DateTime.Now;
            UmbracoRequestId = Guid.NewGuid();
            HttpContext = httpContext;
            Security = webSecurity;

            // beware - we cannot expect a current user here, so detecting preview mode must be a lazy thing
            _facade = new Lazy<IFacade>(() => facadeService.CreateFacade(PreviewToken));

            // set the urls...
            // NOTE: The request will not be available during app startup so we can only set this to an absolute URL of localhost, this
            // is a work around to being able to access the UmbracoContext during application startup and this will also ensure that people
            // 'could' still generate URLs during startup BUT any domain driven URL generation will not work because it is NOT possible to get
            // the current domain during application startup.
            // see: http://issues.umbraco.org/issue/U4-1890
            //
            OriginalRequestUrl = GetRequestFromContext()?.Url ?? new Uri("http://localhost");
            CleanedUmbracoUrl = UriUtility.UriToUmbraco(OriginalRequestUrl);
            UrlProvider = new UrlProvider(this, umbracoSettings.WebRouting, urlProviders);
        }

        #endregion

        /// <summary>
        /// Gets the current Umbraco Context.
        /// </summary>
        // note: obsolete, use Current.UmbracoContext... then obsolete Current too, and inject!
        public static UmbracoContext Current => Composing.Current.UmbracoContext;

        /// <summary>
        /// This is used internally for performance calculations, the ObjectCreated DateTime is set as soon as this
        /// object is instantiated which in the web site is created during the BeginRequest phase.
        /// We can then determine complete rendering time from that.
        /// </summary>
        internal DateTime ObjectCreated { get; private set; }

        /// <summary>
        /// This is used internally for debugging and also used to define anything required to distinguish this request from another.
        /// </summary>
        internal Guid UmbracoRequestId { get; private set; }

        /// <summary>
        /// Gets the WebSecurity class
        /// </summary>
        public WebSecurity Security { get; }

        /// <summary>
        /// Gets the uri that is handled by ASP.NET after server-side rewriting took place.
        /// </summary>
        internal Uri OriginalRequestUrl { get; }

        /// <summary>
        /// Gets the cleaned up url that is handled by Umbraco.
        /// </summary>
        /// <remarks>That is, lowercase, no trailing slash after path, no .aspx...</remarks>
        internal Uri CleanedUmbracoUrl { get; private set; }

        /// <summary>
        /// Gets the facade.
        /// </summary>
        public IFacade Facade => _facade.Value;

        // for unit tests
        internal bool HasFacade => _facade.IsValueCreated;

        /// <summary>
        /// Gets the published content cache.
        /// </summary>
        public IPublishedContentCache ContentCache => Facade.ContentCache;

        /// <summary>
        /// Gets the published media cache.
        /// </summary>
        public IPublishedMediaCache MediaCache => Facade.MediaCache;

        /// <summary>
        /// Boolean value indicating whether the current request is a front-end umbraco request
        /// </summary>
        public bool IsFrontEndUmbracoRequest => PublishedContentRequest != null;

        /// <summary>
        /// Gets the url provider.
        /// </summary>
        public UrlProvider UrlProvider { get; }

        /// <summary>
        /// Gets/sets the PublishedContentRequest object
        /// </summary>
        public PublishedContentRequest PublishedContentRequest { get; set; }

        /// <summary>
        /// Exposes the HttpContext for the current request
        /// </summary>
        public HttpContextBase HttpContext { get; }

        /// <summary>
        /// Gets a value indicating whether the request has debugging enabled
        /// </summary>
        /// <value><c>true</c> if this instance is debug; otherwise, <c>false</c>.</value>
        public bool IsDebug
        {
            get
            {
                var request = GetRequestFromContext();
                //NOTE: the request can be null during app startup!
                return GlobalSettings.DebugMode
                    && request != null
                    && (string.IsNullOrEmpty(request["umbdebugshowtrace"]) == false
                        || string.IsNullOrEmpty(request["umbdebug"]) == false);
            }
        }

        /// <summary>
        /// Gets the current page ID, or <c>null</c> if no page ID is available (e.g. a custom page).
        /// </summary>
        public int? PageId
        {
            // TODO - this is dirty old legacy tricks, we should clean it up at some point
            // also, what is a "custom page" and when should this be either null, or different
            // from PublishedContentRequest.PublishedContent.Id ??
            // SD: Have found out it can be different when rendering macro contents in the back office, but really youshould just be able
            // to pass a page id to the macro renderer instead but due to all the legacy bits that's real difficult.
            get
            {
                try
                {
                    //TODO: this should be done with a wrapper: http://issues.umbraco.org/issue/U4-61
                    return int.Parse(HttpContext.Items["pageID"].ToString());
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Determines whether the current user is in a preview mode and browsing the site (ie. not in the admin UI)
        /// </summary>
        public bool InPreviewMode
        {
            get
            {
                if (_previewing.HasValue == false) DetectPreviewMode();
                return _previewing.Value;
            }
            private set { _previewing = value; }
        }

        private string PreviewToken
        {
            get
            {
                if (_previewing.HasValue == false) DetectPreviewMode();
                return _previewToken;
            }
        }

        private void DetectPreviewMode()
        {
            var request = GetRequestFromContext();
            if (request?.Url != null
                && request.Url.IsBackOfficeRequest(HttpRuntime.AppDomainAppVirtualPath) == false
                && Security.CurrentUser != null)
            {
                var previewToken = request.GetPreviewCookieValue(); // may be null or empty
                _previewToken = previewToken.IsNullOrWhiteSpace() ? null : previewToken;
            }

            _previewing = _previewToken.IsNullOrWhiteSpace() == false;
        }

        // say we render a macro or RTE in a give 'preview' mode that might not be the 'current' one,
        // then due to the way it all works at the moment, the 'current' facade need to be in the proper
        // default 'preview' mode - somehow we have to force it. and that could be recursive.
        internal IDisposable ForcedPreview(bool preview)
        {
            InPreviewMode = preview;
            return Facade.ForcedPreview(preview, orig => InPreviewMode = orig);
        }

        private HttpRequestBase GetRequestFromContext()
        {
            try
            {
                return HttpContext.Request;
            }
            catch (HttpException)
            {
                return null;
            }
        }

        protected override void DisposeResources()
        {
            // DisposableObject ensures that this runs only once

            Security.DisposeIfDisposable();

            // reset - important when running outside of http context
            // also takes care of the accessor
            Composing.Current.ClearUmbracoContext();

            // help caches release resources
            // (but don't create caches just to dispose them)
            // context is not multi-threaded
            if (_facade.IsValueCreated)
                _facade.Value.DisposeIfDisposable();
        }
    }
}
