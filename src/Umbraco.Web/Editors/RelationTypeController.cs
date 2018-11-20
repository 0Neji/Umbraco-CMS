﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using Umbraco.Core.Models;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using Umbraco.Web.WebApi.Filters;
using Constants = Umbraco.Core.Constants;

namespace Umbraco.Web.Editors
{
    /// <summary>
    /// The API controller for editing relation types.
    /// </summary>
    [PluginController("UmbracoApi")]
    [UmbracoTreeAuthorize(Constants.Trees.RelationTypes)]
    [EnableOverrideAuthorization]
    public class RelationTypeController : BackOfficeNotificationsController
    {
        /// <summary>
        /// Gets a relation type by ID.
        /// </summary>
        /// <param name="id">The relation type ID.</param>
        /// <returns>Returns the <see cref="RelationTypeDisplay"/>.</returns>
        public RelationTypeDisplay GetById(int id)
        {
            var relationType = Services.RelationService.GetRelationTypeById(id);

            if (relationType == null)
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }
                        
            var relations = Services.RelationService.GetByRelationTypeId(relationType.Id);

            var display = Mapper.Map<IRelationType, RelationTypeDisplay>(relationType);
            display.Relations = Mapper.Map<IEnumerable<IRelation>, IEnumerable<RelationDisplay>>(relations);

            return display;
        }

        public RelationTypeDisplay PostSave(RelationTypeSave relationType)
        {
            var relationTypePersisted = Services.RelationService.GetRelationTypeById(relationType.Key);

            if (relationTypePersisted == null)
            {
                // TODO: Translate message
                throw new HttpResponseException(Request.CreateNotificationValidationErrorResponse("Relation type does not exist"));
            }

            Mapper.Map(relationType, relationTypePersisted);

            try
            {
                Services.RelationService.Save(relationTypePersisted);
                var display = Mapper.Map<RelationTypeDisplay>(relationTypePersisted);
                display.AddSuccessNotification("Relation type saved", "");

                return display;
            }
            catch (Exception ex)
            {
                Logger.Error(GetType(), ex, "Error saving relation type with {Id}", relationType.Id);
                throw new HttpResponseException(Request.CreateNotificationValidationErrorResponse("Something went wrong when saving the relation type"));
            }
        }

        public void DeleteById()
        {
            throw new NotImplementedException();
        }
    }
}
