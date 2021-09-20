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
    public class OutputBindingTests : IntegrationTestBase
    {
        public OutputBindingTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task SendOutputRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = @"http://localhost:7071/api/" + functionName;

            if (query != null)
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, query);
            }

            await SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost)
        {
            Dictionary<string, string> query = new Dictionary<string, string>()
            {
                { "EmployeeId", id.ToString() },
                { "Name", name },
                { "Cost", cost.ToString() }
            };

            SendOutputRequest(nameof(SqlExtensionSamples.AddProduct), query).Wait();
        }
    }
}
