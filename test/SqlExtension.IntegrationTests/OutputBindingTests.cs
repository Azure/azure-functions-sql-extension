using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SqlExtension.IntegrationTests
{
    public class OutputBindingTests : IntegrationTestBase
    {
        public OutputBindingTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendOutputRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = $"http://localhost:{Port}/api/{functionName}";

            if (query != null)
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, query);
            }

            return await SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost)
        {
            Dictionary<string, string> query = new Dictionary<string, string>()
            {
                { "ProductID", id.ToString() },
                { "Name", name },
                { "Cost", cost.ToString() }
            };

            SendOutputRequest(nameof(SqlExtensionSamples.AddProduct), query).Wait();

            // Verify result
            Assert.Equal(name, ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Fact]
        public void AddProductArrayTest()
        {
            // First insert some test data
            ExecuteNonQuery("INSERT INTO Products VALUES (1, 'test', 100)");
            ExecuteNonQuery("INSERT INTO Products VALUES (2, 'test', 100)");
            ExecuteNonQuery("INSERT INTO Products VALUES (3, 'test', 100)");

            SendOutputRequest("addproducts-array").Wait();

            // Function call changes first 2 rows to (1, 'Cup', 2) and (2, 'Glasses', 12)
            Assert.Equal(1, ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Cost = 100"));
            Assert.Equal(2, ExecuteScalar("SELECT Cost FROM Products WHERE ProductId = 1"));
            Assert.Equal(2, ExecuteScalar("SELECT ProductId FROM Products WHERE Cost = 12"));
        }

        [Fact]
        public void AddProductsCollectorTest()
        {
            // Function should add 5000 rows to the table
            SendOutputRequest("addproducts-collector").Wait();

            Assert.Equal(5000, ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Fact]
        public void QueueTriggerProductsTest()
        {
            string uri = $"http://localhost:{Port}/admin/functions/QueueTriggerProducts";
            string json = "{ 'input': 'Test Data' }";

            SendPostRequest(uri, json).Wait();

            Thread.Sleep(5000);

            // Function should add 100 rows
            Assert.Equal(100, ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }
    }
}
