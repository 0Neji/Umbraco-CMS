﻿using System;
using NPoco;
using Umbraco.Core.Persistence.DatabaseAnnotations;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;

namespace Umbraco.Core.Persistence.Dtos
{
    [TableName(Constants.DatabaseSchema.Tables.ExternalLogin)]
    [ExplicitColumns]
    [PrimaryKey("Id")]
    internal class ExternalLoginDto
    {
        [Column("id")]
        [PrimaryKeyColumn(Name = "PK_umbracoExternalLogin")]
        public int Id { get; set; }

        [Column("userId")]
        public int UserId { get; set; }

        [Column("loginProvider")]
        [Length(4000)]
        [NullSetting(NullSetting = NullSettings.NotNull)]
        public string LoginProvider { get; set; }

        [Column("providerKey")]
        [Length(4000)]
        [NullSetting(NullSetting = NullSettings.NotNull)]
        public string ProviderKey { get; set; }

        [Column("createDate")]
        [Constraint(Default = SystemMethods.CurrentDateTime)]
        public DateTime CreateDate { get; set; }
    }
}
