﻿using System;
using System.Collections.Generic;
using System.Data;
using DbUp.Engine;
using DbUp.Engine.Output;
using NSubstitute;
using NUnit.Framework;

namespace DbUp.Tests.Engine.Transactions
{
    [TestFixture]
    public class DatabaseConnectionManagerTests
    {
        private readonly TestConnectionManager sut;
        private readonly IDbConnection connection;

        public DatabaseConnectionManagerTests()
        {
            connection = Substitute.For<IDbConnection>();
            sut = new TestConnectionManager(connection);
        }

        [Test]
        public void should_dispose_connection_on_dispose()
        {
            using (sut.OperationStarting(new ConsoleUpgradeLog(), new List<SqlScript>())){}

            connection.Received(1).Dispose();
        }
    }
}