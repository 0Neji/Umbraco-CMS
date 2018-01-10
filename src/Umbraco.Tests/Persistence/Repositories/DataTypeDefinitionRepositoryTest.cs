﻿using System;
using System.Linq;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Tests.TestHelpers;
using Umbraco.Core.Composing;
using Umbraco.Tests.Testing;
using LightInject;
using Umbraco.Core.Persistence.Dtos;
using Umbraco.Core.Persistence.Repositories.Implement;
using Umbraco.Core.Scoping;

namespace Umbraco.Tests.Persistence.Repositories
{
    [TestFixture]
    [UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
    public class DataTypeDefinitionRepositoryTest : TestWithDatabaseBase
    {
        //protected override CacheHelper CreateCacheHelper()
        //{
        //    // hackish, but it works
        //    var testName = TestContext.CurrentContext.Test.Name;
        //    if (testName == "Can_Get_Pre_Value_As_String_With_Cache"
        //        || testName == "Can_Get_Pre_Value_Collection_With_Cache")
        //    {
        //        return new CacheHelper(
        //            new ObjectCacheRuntimeCacheProvider(),
        //            new StaticCacheProvider(),
        //            new StaticCacheProvider(),
        //            new IsolatedRuntimeCache(type => new ObjectCacheRuntimeCacheProvider())); // default would be NullCacheProvider
        //    }

        //    return base.CreateCacheHelper();
        //}

        protected override void ComposeCacheHelper()
        {
            // hackish, but it works
            var testName = TestContext.CurrentContext.Test.Name;
            if (testName == "Can_Get_Pre_Value_As_String_With_Cache" || testName == "Can_Get_Pre_Value_Collection_With_Cache")
            {
                var cacheHelper = new CacheHelper(
                    new ObjectCacheRuntimeCacheProvider(),
                    new StaticCacheProvider(),
                    new StaticCacheProvider(),
                    new IsolatedRuntimeCache(type => new ObjectCacheRuntimeCacheProvider())); // default would be NullCacheProvider

                Container.RegisterSingleton(f => cacheHelper);
                Container.RegisterSingleton(f => f.GetInstance<CacheHelper>().RuntimeCache);
            }
            else
            {
                base.ComposeCacheHelper();
            }
        }

        private IDataTypeRepository CreateRepository()
        {
            return Container.GetInstance<IDataTypeRepository>();
        }

        private EntityContainerRepository CreateContainerRepository(IScopeAccessor scopeAccessor)
        {
            return new EntityContainerRepository(scopeAccessor, CacheHelper.CreateDisabledCacheHelper(), Logger, Constants.ObjectTypes.DataTypeContainer);
        }

        [Test]
        public void Can_Move()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository(accessor);
                var repository = CreateRepository();
                var container1 = new EntityContainer(Constants.ObjectTypes.DataType) { Name = "blah1" };
                containerRepository.Save(container1);

                var container2 = new EntityContainer(Constants.ObjectTypes.DataType) { Name = "blah2", ParentId = container1.Id };
                containerRepository.Save(container2);

                var dataType = (IDataType)new DataType(container2.Id, Constants.PropertyEditors.RadioButtonListAlias)
                {
                    Name = "dt1"
                };
                repository.Save(dataType);

                //create a
                var dataType2 = (IDataType)new DataType(dataType.Id, Constants.PropertyEditors.RadioButtonListAlias)
                {
                    Name = "dt2"
                };
                repository.Save(dataType2);

                var result = repository.Move(dataType, container1).ToArray();

                Assert.AreEqual(2, result.Count());

                //re-get
                dataType = repository.Get(dataType.Id);
                dataType2 = repository.Get(dataType2.Id);

                Assert.AreEqual(container1.Id, dataType.ParentId);
                Assert.AreNotEqual(result.Single(x => x.Entity.Id == dataType.Id).OriginalPath, dataType.Path);
                Assert.AreNotEqual(result.Single(x => x.Entity.Id == dataType2.Id).OriginalPath, dataType2.Path);
            }
        }

        [Test]
        public void Can_Create_Container()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository(accessor);
                var container = new EntityContainer(Constants.ObjectTypes.DataType) { Name = "blah" };
                containerRepository.Save(container);

                Assert.That(container.Id, Is.GreaterThan(0));

                var found = containerRepository.Get(container.Id);
                Assert.IsNotNull(found);
            }
        }

        [Test]
        public void Can_Delete_Container()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository(accessor);
                var container = new EntityContainer(Constants.ObjectTypes.DataType) { Name = "blah" };
                containerRepository.Save(container);

                // Act
                containerRepository.Delete(container);

                var found = containerRepository.Get(container.Id);
                Assert.IsNull(found);
            }
        }

        [Test]
        public void Can_Create_Container_Containing_Data_Types()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository(accessor);
                var repository = CreateRepository();
                var container = new EntityContainer(Constants.ObjectTypes.DataType) { Name = "blah" };
                containerRepository.Save(container);

                var dataTypeDefinition = new DataType(container.Id, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.Save(dataTypeDefinition);

                Assert.AreEqual(container.Id, dataTypeDefinition.ParentId);
            }
        }

        [Test]
        public void Can_Delete_Container_Containing_Data_Types()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var containerRepository = CreateContainerRepository(accessor);
                var repository = CreateRepository();
                var container = new EntityContainer(Constants.ObjectTypes.DataType) { Name = "blah" };
                containerRepository.Save(container);

                IDataType dataType = new DataType(container.Id, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.Save(dataType);

                // Act
                containerRepository.Delete(container);

                var found = containerRepository.Get(container.Id);
                Assert.IsNull(found);

                dataType = repository.Get(dataType.Id);
                Assert.IsNotNull(dataType);
                Assert.AreEqual(-1, dataType.ParentId);
            }
        }

        [Test]
        public void Can_Create()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();
                IDataType dataType = new DataType(-1, Constants.PropertyEditors.RadioButtonListAlias) {Name = "test"};

                repository.Save(dataType);

                var id = dataType.Id;
                Assert.That(id, Is.GreaterThan(0));

                // Act
                dataType = repository.Get(id);

                // Assert
                Assert.That(dataType, Is.Not.Null);
                Assert.That(dataType.HasIdentity, Is.True);
            }
        }


        [Test]
        public void Can_Perform_Get_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();
                // Act
                var dataTypeDefinition = repository.Get(-42);

                // Assert
                Assert.That(dataTypeDefinition, Is.Not.Null);
                Assert.That(dataTypeDefinition.HasIdentity, Is.True);
                Assert.That(dataTypeDefinition.Name, Is.EqualTo("Dropdown"));
            }
        }

        [Test]
        public void Can_Perform_GetAll_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();

                // Act
                var dataTypeDefinitions = repository.GetMany();

                // Assert
                Assert.That(dataTypeDefinitions, Is.Not.Null);
                Assert.That(dataTypeDefinitions.Any(), Is.True);
                Assert.That(dataTypeDefinitions.Any(x => x == null), Is.False);
                Assert.That(dataTypeDefinitions.Count(), Is.EqualTo(24));
            }
        }

        [Test]
        public void Can_Perform_GetAll_With_Params_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();

                // Act
                var dataTypeDefinitions = repository.GetMany(-40, -41, -42);

                // Assert
                Assert.That(dataTypeDefinitions, Is.Not.Null);
                Assert.That(dataTypeDefinitions.Any(), Is.True);
                Assert.That(dataTypeDefinitions.Any(x => x == null), Is.False);
                Assert.That(dataTypeDefinitions.Count(), Is.EqualTo(3));
            }
        }

        [Test]
        public void Can_Perform_GetByQuery_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();

                // Act
                var query = scope.SqlContext.Query<IDataType>().Where(x => x.EditorAlias == Constants.PropertyEditors.RadioButtonListAlias);
                var result = repository.Get(query);

                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Any(), Is.True);
                Assert.That(result.FirstOrDefault().Name, Is.EqualTo("Radiobox"));
            }
        }

        [Test]
        public void Can_Perform_Count_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();

                // Act
                var query = scope.SqlContext.Query<IDataType>().Where(x => x.Name.StartsWith("D"));
                int count = repository.Count(query);

                // Assert
                Assert.That(count, Is.EqualTo(4));
            }
        }

        [Test]
        public void Can_Perform_Add_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();
                var dataTypeDefinition = new DataType("Test.TestEditor")
                {
                    DatabaseType = DataTypeDatabaseType.Integer,
                    Name = "AgeDataType",
                    CreatorId = 0
                };

                // Act
                repository.Save(dataTypeDefinition);

                var exists = repository.Exists(dataTypeDefinition.Id);

                var fetched = repository.Get(dataTypeDefinition.Id);

                // Assert
                Assert.That(dataTypeDefinition.HasIdentity, Is.True);
                Assert.That(exists, Is.True);

                TestHelper.AssertPropertyValuesAreEqual(dataTypeDefinition, fetched, "yyyy-MM-dd HH:mm:ss");
            }
        }

        [Test]
        public void Can_Perform_Update_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();
                var dataTypeDefinition = new DataType("Test.blah")
                {
                    DatabaseType = DataTypeDatabaseType.Integer,
                    Name = "AgeDataType",
                    CreatorId = 0
                };
                repository.Save(dataTypeDefinition);

                // Act
                var definition = repository.Get(dataTypeDefinition.Id);
                definition.Name = "AgeDataType Updated";
                definition.EditorAlias = "Test.TestEditor"; //change
                repository.Save(definition);

                var definitionUpdated = repository.Get(dataTypeDefinition.Id);

                // Assert
                Assert.That(definitionUpdated, Is.Not.Null);
                Assert.That(definitionUpdated.Name, Is.EqualTo("AgeDataType Updated"));
                Assert.That(definitionUpdated.EditorAlias, Is.EqualTo("Test.TestEditor"));
            }
        }

        [Test]
        public void Can_Perform_Delete_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();
                var dataTypeDefinition = new DataType("Test.TestEditor")
                {
                    DatabaseType = DataTypeDatabaseType.Integer,
                    Name = "AgeDataType",
                    CreatorId = 0
                };

                // Act
                repository.Save(dataTypeDefinition);

                var existsBefore = repository.Exists(dataTypeDefinition.Id);

                repository.Delete(dataTypeDefinition);

                var existsAfter = repository.Exists(dataTypeDefinition.Id);

                // Assert
                Assert.That(existsBefore, Is.True);
                Assert.That(existsAfter, Is.False);
            }
        }

        [Test]
        public void Can_Perform_Exists_On_DataTypeDefinitionRepository()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();

                // Act
                var exists = repository.Exists(1046); //Content picker
                var doesntExist = repository.Exists(-80);

                // Assert
                Assert.That(exists, Is.True);
                Assert.That(doesntExist, Is.False);
            }
        }

        [Test]
        public void Can_Get_Pre_Value_Collection()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();
                var dataTypeDefinition = new DataType(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.Save(dataTypeDefinition);

                var dtid = dataTypeDefinition.Id;

                scope.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 0, Value = "test1"});
                scope.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 1, Value = "test2" });

                var collection = repository.GetPreValuesCollectionByDataTypeId(dtid);
                Assert.AreEqual(2, collection.PreValuesAsArray.Count());
            }
        }

        [Test]
        public void Can_Get_Pre_Value_As_String()
        {
            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = CreateRepository();
                var dataTypeDefinition = new DataType(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.Save(dataTypeDefinition);

                var dtid = dataTypeDefinition.Id;

                var id = scope.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 0, Value = "test1" });
                scope.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtid, SortOrder = 1, Value = "test2" });

                var val = repository.GetPreValueAsString(Convert.ToInt32(id));
                Assert.AreEqual("test1", val);
            }
        }

        [Test]
        public void Can_Get_Pre_Value_Collection_With_Cache()
        {
            DataType dtd;

            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = Container.GetInstance<IDataTypeRepository>();
                dtd = new DataType(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.Save(dtd);

                scope.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtd.Id, SortOrder = 0, Value = "test1" });
                scope.Database.Insert(new DataTypePreValueDto { DataTypeNodeId = dtd.Id, SortOrder = 1, Value = "test2" });

                //this will cache the result
                var collection = repository.GetPreValuesCollectionByDataTypeId(dtd.Id);
            }

            // note: see CreateCacheHelper, this test uses a special cache
            var cache = CacheHelper.IsolatedRuntimeCache.GetCache<IDataType>();
            Assert.IsTrue(cache);
            var cached = cache.Result
                .GetCacheItemsByKeySearch<PreValueCollection>(CacheKeys.DataTypePreValuesCacheKey + "_" + dtd.Id);

            Assert.IsNotNull(cached);
            Assert.AreEqual(1, cached.Count());
            Assert.AreEqual(2, cached.Single().FormatAsDictionary().Count);
        }

        [Test]
        public void Can_Get_Pre_Value_As_String_With_Cache()
        {
            DataType dtd;
            object id;

            var provider = TestObjects.GetScopeProvider(Logger);
            var accessor = (IScopeAccessor) provider;

            using (var scope = provider.CreateScope())
            {
                var repository = Container.GetInstance<IDataTypeRepository>();
                dtd = new DataType(-1, Constants.PropertyEditors.RadioButtonListAlias) { Name = "test" };
                repository.Save(dtd);

                id = scope.Database.Insert(new DataTypePreValueDto() { DataTypeNodeId = dtd.Id, SortOrder = 0, Value = "test1" });
                scope.Database.Insert(new DataTypePreValueDto() { DataTypeNodeId = dtd.Id, SortOrder = 1, Value = "test2" });

                //this will cache the result
                var val = repository.GetPreValueAsString(Convert.ToInt32(id));
            }

            // note: see CreateCacheHelper, this test uses a special cache
            var cache = CacheHelper.IsolatedRuntimeCache.GetCache<IDataType>();
            Assert.IsTrue(cache);
            var cached = cache.Result
                .GetCacheItemsByKeySearch<PreValueCollection>(CacheKeys.DataTypePreValuesCacheKey + "_" + dtd.Id);

            Assert.IsNotNull(cached);
            Assert.AreEqual(1, cached.Count());
            Assert.AreEqual(2, cached.Single().FormatAsDictionary().Count);

            using (var scope = provider.CreateScope())
            {
                var repository = Container.GetInstance<IDataTypeRepository>();
                //ensure it still gets resolved!
                var val = repository.GetPreValueAsString(Convert.ToInt32(id));
                Assert.AreEqual("test1", val);
            }
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }
    }
}
