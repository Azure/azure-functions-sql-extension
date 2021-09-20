using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SqlExtensionSamples;
using static SqlExtensionSamples.ProductUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SqlExtension.IntegrationTests
{
    public class InputBindingTests : IntegrationTestBase
    {
        public InputBindingTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task SendInputRequest(string functionName, string query = "")
        {
            string requestUri = $"http://localhost:7071/api/{functionName}/{query}";

            await SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, -500)]
        [InlineData(100, 500)]
        public void GetProductsByCostTest(int n, int cost)
        {
            // Generate T-SQL to insert n rows of data with cost
            StringBuilder queryBuilder = new StringBuilder();
            for (int i = 1; i <= n; i++)
            {
                queryBuilder.AppendLine($"INSERT INTO dbo.Products VALUES({i}, 'test', {cost});");
            }

            // Run the query
            if (!string.IsNullOrEmpty(queryBuilder.ToString()))
            {
                ExecuteNonQuery(queryBuilder.ToString());
            }
            
            // Run the function
            SendInputRequest(nameof(SqlExtensionSamples.GetProductsByCost), n.ToString()).Wait();

            // Verify result
            Assert.Equal(n, ExecuteScalar($"select count(1) from Products where Cost = {cost}"));
        }
    }
}
