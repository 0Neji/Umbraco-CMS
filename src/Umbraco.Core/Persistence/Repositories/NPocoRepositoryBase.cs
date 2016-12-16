﻿using System;
using System.Collections.Generic;
using NPoco;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.Repositories
{
    /// <summary>
    /// Represent an abstract Repository for NPoco based repositories
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TEntity"></typeparam>
    internal abstract class NPocoRepositoryBase<TId, TEntity> : RepositoryBase<TId, TEntity>
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NPocoRepositoryBase{TId, TEntity}"/>.
        /// </summary>
        /// <param name="work">A database unit of work.</param>
        /// <param name="cache">A cache helper.</param>
        /// <param name="logger">A logger.</param>
        protected NPocoRepositoryBase(IDatabaseUnitOfWork work, CacheHelper cache, ILogger logger)
            : base(work, cache, logger)
        { }

        /// <summary>
		/// Gets the repository's unit of work.
		/// </summary>
		protected internal new IDatabaseUnitOfWork UnitOfWork => (IDatabaseUnitOfWork) base.UnitOfWork;

        /// <summary>
        /// Gets the repository's database.
        /// </summary>
        protected IUmbracoDatabase Database => UnitOfWork.Database;

        /// <summary>
        /// Gets the repository's database sql syntax.
        /// </summary>
        public ISqlSyntaxProvider SqlSyntax => UnitOfWork.SqlSyntax;

        /// <summary>
        /// Creates a new query.
        /// </summary>
        public override IQuery<T> Query<T>() => UnitOfWork.Query<T>();

        /// <summary>
        /// Creates a new Sql statement.
        /// </summary>
        /// <returns>A new Sql statement.</returns>
        protected Sql<SqlContext> Sql() => UnitOfWork.Sql();

        #region Abstract Methods

        protected abstract Sql<SqlContext> GetBaseQuery(bool isCount);
        protected abstract string GetBaseWhereClause();
        protected abstract IEnumerable<string> GetDeleteClauses();
        protected abstract Guid NodeObjectTypeId { get; }
        protected abstract override void PersistNewItem(TEntity entity);
        protected abstract override void PersistUpdatedItem(TEntity entity);

        #endregion

        protected override bool PerformExists(TId id)
        {
            var sql = GetBaseQuery(true);
            sql.Where(GetBaseWhereClause(), new { Id = id});
            var count = Database.ExecuteScalar<int>(sql);
            return count == 1;
        }

        protected override int PerformCount(IQuery<TEntity> query)
        {
            var sqlClause = GetBaseQuery(true);
            var translator = new SqlTranslator<TEntity>(sqlClause, query);
            var sql = translator.Translate();

            return Database.ExecuteScalar<int>(sql);
        }

        protected override void PersistDeletedItem(TEntity entity)
        {
            var deletes = GetDeleteClauses();
            foreach (var delete in deletes)
            {
                Database.Execute(delete, new { Id = GetEntityId(entity) });
            }
        }

        protected new virtual TId GetEntityId(TEntity entity)
        {
            return (TId)(object) entity.Id;
        }
    }
}