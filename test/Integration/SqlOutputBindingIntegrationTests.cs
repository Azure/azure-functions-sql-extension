// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
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
            this.StartFunctionHost(nameof(AddProduct));

            var query = new Dictionary<string, string>()
            {
                { "id", id.ToString() },
                { "name", name },
                { "cost", cost.ToString() }
            };

            this.SendOutputRequest(nameof(AddProduct), query).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Fact]
        public void AddProductArrayTest()
        {
            this.StartFunctionHost(nameof(AddProductsArray));

            // First insert some test data
            this.ExecuteNonQuery("INSERT INTO Products VALUES (1, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (2, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (3, 'test', 100)");

            this.SendOutputRequest("addproducts-array").Wait();

            // Function call changes first 2 rows to (1, 'Cup', 2) and (2, 'Glasses', 12)
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Cost = 100"));
            Assert.Equal(2, this.ExecuteScalar("SELECT Cost FROM Products WHERE ProductId = 1"));
            Assert.Equal(2, this.ExecuteScalar("SELECT ProductId FROM Products WHERE Cost = 12"));
        }

        [Fact]
        public void AddProductsCollectorTest()
        {
            this.StartFunctionHost(nameof(AddProductsCollector));

            // Function should add 5000 rows to the table
            this.SendOutputRequest("addproducts-collector").Wait();

            Assert.Equal(5000, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Fact]
        public void QueueTriggerProductsTest()
        {
            this.StartFunctionHost(nameof(QueueTriggerProducts));

            string uri = $"http://localhost:{this.Port}/admin/functions/QueueTriggerProducts";
            string json = "{ 'input': 'Test Data' }";

            this.SendPostRequest(uri, json).Wait();

            Thread.Sleep(5000);

            // Function should add 100 rows
            Assert.Equal(100, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Fact]
        public void TimerTriggerProductsTest()
        {
            // Timer trigger function requires local storage to be running
            this.StartAzurite();
            this.StartFunctionHost(nameof(TimerTriggerProducts));

            // Since this function runs on a schedule (every 5 seconds), we don't need to invoke it.
            // We will wait 6 seconds to guarantee that it has been fired at least once, and check that at least 1000 rows of data has been added.
            Thread.Sleep(6000);

            int rowsAdded = (int)this.ExecuteScalar("SELECT COUNT(1) FROM Products");
            Assert.True(rowsAdded >= 1000);
        }
    }
}
