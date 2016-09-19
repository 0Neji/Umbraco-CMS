﻿using System.Web;
using Umbraco.Core.Events;
using Umbraco.Core.Persistence.Migrations;
using Umbraco.Web.WebApi.Filters;
using Umbraco.Core.Configuration;

namespace Umbraco.Web.Strategies.Migrations
{
    /// <summary>
    /// After upgrade we clear out the csrf tokens
    /// </summary>
    public class ClearCsrfCookiesAfterUpgrade : IPostMigration
    {
        public void Migrated(MigrationRunner sender, MigrationEventArgs args)
        {
            if (args.ProductName != GlobalSettings.UmbracoMigrationName) return;
            if (HttpContext.Current == null) return;

            var http = new HttpContextWrapper(HttpContext.Current);
            http.ExpireCookie(AngularAntiForgeryHelper.AngularCookieName);
            http.ExpireCookie(AngularAntiForgeryHelper.CsrfValidationCookieName);
        }
    }
}