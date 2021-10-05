// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SqlExtension.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlOutputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlOutputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendOutputRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            if (query != null)
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, query);
            }

            return await this.SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost)
        {
            var query = new Dictionary<string, string>()
            {
                { "id", id.ToString() },
                { "name", name },
                { "cost", cost.ToString() }
            };

            this.SendOutputRequest(nameof(SqlExtensionSamples.AddProduct), query).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Fact]
        public void AddProductArrayTest()
        {
            // First insert some test data
            this.ExecuteNonQuery("INSERT INTO Products VALUES (1, 'test', 1000)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (2, 'test', 1000)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (3, 'test', 1000)");

            this.SendOutputRequest("addproducts-array").Wait();

            // Function call changes first 2 rows to (1, 'Cup', 2) and (2, 'Glasses', 12)
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Cost = 1000"));
            Assert.Equal(2, this.ExecuteScalar("SELECT Cost FROM Products WHERE ProductId = 1"));
            Assert.Equal(2, this.ExecuteScalar("SELECT ProductId FROM Products WHERE Cost = 12"));
        }

        [Fact]
        public void AddProductsCollectorTest()
        {
            // Function should add 5000 rows to the table
            this.SendOutputRequest("addproducts-collector").Wait();

            // Excluding 10000+ ProductIds which could be added by the Timer Function during this time
            Assert.Equal(5000, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE ProductId < 10000"));
        }

        [Fact]
        public void QueueTriggerProductsTest()
        {
            string uri = $"http://localhost:{this.Port}/admin/functions/QueueTriggerProducts";
            string json = "{ 'input': 'Test Data' }";

            this.SendPostRequest(uri, json).Wait();

            Thread.Sleep(5000);

            // Function should add 100 rows (excluding ProductId >= 10000 which could be added by the timer trigger function)
            Assert.Equal(100, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE ProductId < 10000"));
        }

        [Fact]
        public void TimerTriggerProductsTest()
        {
            // Since this function runs on a schedule (every 30 seconds), we don't need to invoke it.
            // However the timer starts as soon as the Functions host starts, so we need to take that into account when calculating how long to wait.
            // In total we will wait 60 seconds + 1 ms which should allow the function to run twice, generating 2000 rows of data.

            //string uri = $"http://localhost:{this.Port}/admin/functions/TimerTriggerProducts";
            //string json = "{ 'input': 'Test Data' }";

            //this.SendPostRequest(uri, json).Wait();

            TimeSpan elapsed = DateTime.Now.Subtract(this.FunctionHost.StartTime);
            TimeSpan wait = new TimeSpan(TimeSpan.TicksPerMinute + TimeSpan.TicksPerMillisecond).Subtract(elapsed);

            Thread.Sleep(wait);

            Assert.Equal(3000, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE ProductId >= 10000"));
        }
    }
}
