using System.Collections.Generic;
using Microsoft.AspNet.Identity;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.Identity;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    public class ExternalLoginService : RepositoryService, IExternalLoginService
    {
        public ExternalLoginService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory, ILogger logger, IEventMessagesFactory eventMessagesFactory)
            : base(provider, repositoryFactory, logger, eventMessagesFactory)
        { }

        /// <summary>
        /// Returns all user logins assigned
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IEnumerable<IIdentityUserLogin> GetAll(int userId)
        {
            using (var uow = UowProvider.GetUnitOfWork())
            {
                var repo = uow.CreateRepository<IExternalLoginRepository>();
                return repo.GetByQuery(repo.Query.Where(x => x.UserId == userId));
            }
        }

        /// <summary>
        /// Returns all logins matching the login info - generally there should only be one but in some cases
        /// there might be more than one depending on if an adminstrator has been editing/removing members
        /// </summary>
        /// <param name="login"></param>
        /// <returns></returns>
        public IEnumerable<IIdentityUserLogin> Find(UserLoginInfo login)
        {
            using (var uow = UowProvider.GetUnitOfWork())
            {
                var repo = uow.CreateRepository<IExternalLoginRepository>();
                return repo.GetByQuery(repo.Query
                    .Where(x => x.ProviderKey == login.ProviderKey && x.LoginProvider == login.LoginProvider));
            }
        }

        /// <summary>
        /// Save user logins
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="logins"></param>
        public void SaveUserLogins(int userId, IEnumerable<UserLoginInfo> logins)
        {
            using (var uow = UowProvider.GetUnitOfWork())
            {
                var repo = uow.CreateRepository<IExternalLoginRepository>();
                repo.SaveUserLogins(userId, logins);
                uow.Complete();
            }
        }

        /// <summary>
        /// Deletes all user logins - normally used when a member is deleted
        /// </summary>
        /// <param name="userId"></param>
        public void DeleteUserLogins(int userId)
        {
            using (var uow = UowProvider.GetUnitOfWork())
            {
                var repo = uow.CreateRepository<IExternalLoginRepository>();
                repo.DeleteUserLogins(userId);
                uow.Complete();
            }
        }
    }
}