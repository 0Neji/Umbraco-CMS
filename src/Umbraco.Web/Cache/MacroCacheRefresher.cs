﻿using System;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using umbraco;
using Umbraco.Core.Persistence.Repositories;
using System.Linq;
using Newtonsoft.Json;
using Macro = umbraco.cms.businesslogic.macro.Macro;

namespace Umbraco.Web.Cache
{
    public sealed class MacroCacheRefresher : JsonCacheRefresherBase<MacroCacheRefresher>
    {
        public MacroCacheRefresher(CacheHelper cacheHelper) 
            : base(cacheHelper)
        { }

        #region Define

        protected override MacroCacheRefresher Instance => this;

        public static readonly Guid UniqueId = Guid.Parse("7B1E683C-5F34-43dd-803D-9699EA1E98CA");

        public override Guid RefresherUniqueId => UniqueId;

        public override string Name => "Macro Cache Refresher";

        #endregion

        #region Refresher

        public override void RefreshAll()
        {
            GetAllMacroCacheKeys().ForEach(
                    prefix =>
                    CacheHelper.RuntimeCache.ClearCacheByKeySearch(prefix));

            ClearAllIsolatedCacheByEntityType<IMacro>();

            CacheHelper.RuntimeCache.ClearCacheObjectTypes<MacroCacheContent>();

            base.RefreshAll();
        }

        public override void Refresh(string json)
        {
            var payloads = Deserialize(json);

            payloads.ForEach(payload =>
            {
                GetCacheKeysForAlias(payload.Alias).ForEach(
                    alias =>
                    CacheHelper.RuntimeCache.ClearCacheByKeySearch(alias));

                var macroRepoCache = CacheHelper.IsolatedRuntimeCache.GetCache<IMacro>();
                if (macroRepoCache)
                {
                    macroRepoCache.Result.ClearCacheItem(RepositoryBase.GetCacheIdKey<IMacro>(payload.Id));
                }
            });

            base.Refresh(json);
        }
        
        #endregion

        #region Json

        public class JsonPayload
        {
            public JsonPayload(int id, string alias)
            {
                Id = id;
                Alias = alias;
            }

            public int Id { get; }

            public string Alias { get; }
        }

        private static JsonPayload[] Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<JsonPayload[]>(json);
        }

        internal static string Serialize(params Macro[] macros)
        {
            return JsonConvert.SerializeObject(macros.Select(x => new JsonPayload(x.Id, x.Alias)).ToArray());
        }

        internal static string Serialize(params IMacro[] macros)
        {
            return JsonConvert.SerializeObject(macros.Select(x => new JsonPayload(x.Id, x.Alias)).ToArray());
        }

        #endregion

        #region Helpers

        internal static string[] GetAllMacroCacheKeys()
        {
            return new[]
                {
                    CacheKeys.MacroCacheKey, // umbraco.cms.businesslogic.macro.Macro objects cache
                    CacheKeys.MacroContentCacheKey, // macro render cache
                    //CacheKeys.MacroControlCacheKey,
                    //CacheKeys.MacroHtmlCacheKey,
                    //CacheKeys.MacroHtmlDateAddedCacheKey,
                    //CacheKeys.MacroControlDateAddedCacheKey,
                    CacheKeys.MacroXsltCacheKey, // XsltMacroEngine transforms cache
                };
        }

        internal static string[] GetCacheKeysForAlias(string alias)
        {
            return GetAllMacroCacheKeys().Select(x => x + alias).ToArray();
        }

        public static void ClearMacroContentCache(CacheHelper cacheHelper)
        {
            cacheHelper.RuntimeCache.ClearCacheObjectTypes<MacroCacheContent>();
        }

        #endregion
    }
}