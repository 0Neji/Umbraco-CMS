using NUnit.Framework;
using Umbraco.Tests.TestHelpers;
using Umbraco.Web.Routing;

namespace Umbraco.Tests.Routing
{
	[TestFixture]
	public class ContentFinderByIdTests : BaseWebTest
    {

		[TestCase("/1046", 1046)]
		[TestCase("/1046.aspx", 1046)]		
		public void Lookup_By_Id(string urlAsString, int nodeMatch)
		{
		    var umbracoContext = GetUmbracoContext(urlAsString);
		    var facadeRouter = CreateFacadeRouter();
			var frequest = facadeRouter.CreateRequest(umbracoContext);
            var lookup = new ContentFinderByIdPath(Logger);
		

			var result = lookup.TryFindContent(frequest);

			Assert.IsTrue(result);
			Assert.AreEqual(frequest.PublishedContent.Id, nodeMatch);
		}
	}
}