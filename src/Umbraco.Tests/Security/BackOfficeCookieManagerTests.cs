using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web;
using Microsoft.Owin;
using Moq;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Tests.TestHelpers;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;
using Umbraco.Web.Security.Identity;

namespace Umbraco.Tests.Security
{
    [TestFixture]
    public class BackOfficeCookieManagerTests : TestWithApplicationBase
    {
        [Test]
        public void ShouldAuthenticateRequest_When_Not_Configured()
        {
            //should force app ctx to show not-configured
            ConfigurationManager.AppSettings.Set("umbracoConfigurationStatus", "");

            var umbracoContext = new UmbracoContext(
                Mock.Of<HttpContextBase>(),
                Mock.Of<IFacadeService>(),
                new WebSecurity(Mock.Of<HttpContextBase>(), Current.Services.UserService),
                TestObjects.GetUmbracoSettings(), new List<IUrlProvider>());

            var runtime = Mock.Of<IRuntimeState>(x => x.Level == RuntimeLevel.Install);
            var mgr = new BackOfficeCookieManager(Mock.Of<IUmbracoContextAccessor>(accessor => accessor.UmbracoContext == umbracoContext), runtime);

            var result = mgr.ShouldAuthenticateRequest(Mock.Of<IOwinContext>(), new Uri("http://localhost/umbraco"));

            Assert.IsFalse(result);
        }

        [Test]
        public void ShouldAuthenticateRequest_When_Configured()
        {
            var umbCtx = new UmbracoContext(
                Mock.Of<HttpContextBase>(),
                Mock.Of<IFacadeService>(),
                new WebSecurity(Mock.Of<HttpContextBase>(), Current.Services.UserService),
                TestObjects.GetUmbracoSettings(), new List<IUrlProvider>());

            var runtime = Mock.Of<IRuntimeState>(x => x.Level == RuntimeLevel.Run);
            var mgr = new BackOfficeCookieManager(Mock.Of<IUmbracoContextAccessor>(accessor => accessor.UmbracoContext == umbCtx), runtime);

            var request = new Mock<OwinRequest>();
            request.Setup(owinRequest => owinRequest.Uri).Returns(new Uri("http://localhost/umbraco"));

            var result = mgr.ShouldAuthenticateRequest(
                Mock.Of<IOwinContext>(context => context.Request == request.Object),
                new Uri("http://localhost/umbraco"));

            Assert.IsTrue(result);
        }

        //TODO : Write remaining tests for `ShouldAuthenticateRequest`
    }
}