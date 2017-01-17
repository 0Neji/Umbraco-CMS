﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Umbraco.Core.Persistence;
using Umbraco.Core.Scoping;
using Umbraco.Tests.TestHelpers;

namespace Umbraco.Tests.Scoping
{
    [TestFixture]
    [DatabaseTestBehavior(DatabaseBehavior.EmptyDbFilePerTest)]
    public class LeakTests : BaseDatabaseFactoryTest
    {
        private UmbracoDatabase _database;
        private IDbConnection _connection;

        // setup
        public override void Initialize()
        {
            base.Initialize();

            //// initialization leaves a NoScope around, remove it
            //var scope = DatabaseContext.ScopeProvider.AmbientScope;
            //Assert.IsNotNull(scope);
            //Assert.IsInstanceOf<NoScope>(scope);
            //scope.Dispose();
            Assert.IsNull(DatabaseContext.ScopeProvider.AmbientScope); // gone
        }

        // note: testing this with a test action is pointless as the
        // AfterTest method runs *before* the TearDown methods which
        // are the methods that should cleanup the call context...

        [Test]
        public void LeakTest()
        {
            _database = DatabaseContext.Database; // creates a database

            _database.Execute("CREATE TABLE foo (id INT)"); // opens a connection
            Assert.IsNull(_database.Connection); // is immediately closed

            _database.BeginTransaction(); // opens and maintains a connection

            // the test is leaking a scope with a non-null database
            var contextScope = CallContext.LogicalGetData("Umbraco.Core.Scoping.IScope");
            Assert.IsNotNull(contextScope);
            Assert.IsInstanceOf<NoScope>(CallContext.LogicalGetData("Umbraco.Core.Scoping.IScope"));
            Assert.IsNotNull(((NoScope) contextScope).DatabaseOrNull);
            Assert.AreSame(_database, ((NoScope)contextScope).DatabaseOrNull);

            // save the connection
            _connection = _database.Connection;
            Assert.IsInstanceOf<StackExchange.Profiling.Data.ProfiledDbConnection>(_connection);
            _connection = ((StackExchange.Profiling.Data.ProfiledDbConnection) _connection).InnerConnection;

            // the connection is open
            Assert.IsNotNull(_connection);
            Assert.AreEqual(ConnectionState.Open, _connection.State);
        }

        // need to explicitely do it in every test which kinda defeats
        // the purposes of having an automated check? give me v8!

        private static void AssertSafeCallContext()
        {
            var scope = CallContext.LogicalGetData("Umbraco.Core.Scoping.IScope");
            if (scope != null) throw new Exception("Leaked call context scope.");
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();

            // the leaked scope should be gone
            AssertSafeCallContext();

            // its database should have been disposed
            Assert.IsNull(_database.Connection);

            // the underlying connection should have been closed
            Assert.AreEqual(ConnectionState.Closed, _connection.State);
        }
    }
}
