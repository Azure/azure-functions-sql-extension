// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
using Newtonsoft.Json;
using BenchmarkDotNet.Attributes;


namespace Microsoft.Azure.WebJobs.Extensions.Sql.Benchmark
{
    public class SqlOutputBindingBenchmarks : IntegrationTestBase
    {
        [GlobalSetup(Target = nameof(AddProductsArrayTest))]
        public void StartAddProductsArrayFunction()
        {
            this.StartFunctionHost(nameof(AddProductsArray), SupportedLanguages.CSharp);
        }

        [GlobalSetup(Target = nameof(AddInvoicesArrayTest))]
        public void StartAddInvoicesArrayFunction()
        {
            this.StartFunctionHost(nameof(AddInvoicesArray), SupportedLanguages.CSharp);
        }

        [Benchmark]
        [Arguments(1)]
        [Arguments(10)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task<HttpResponseMessage> AddProductsArrayTest(int count)
        {
            Product[] productsToAdd = GetProductsWithSameCost(count, 100);
            return await this.SendOutputPostRequest("addproducts-array", JsonConvert.SerializeObject(productsToAdd));
        }

        [Benchmark]
        [Arguments(1)]
        [Arguments(10)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task<HttpResponseMessage> AddInvoicesArrayTest(int count)
        {
            Invoice[] invoicessToAdd = TestUtils.GetInvoices(count);
            return await this.SendOutputPostRequest("addinvoices-array", JsonConvert.SerializeObject(invoicessToAdd));
        }

        [IterationCleanup(Target = nameof(AddProductsArrayTest))]
        public void TruncateProductsTable()
        {
            // Delete all rows in Products table after each iteration
            this.ExecuteNonQuery("TRUNCATE TABLE Products");
        }

        [IterationCleanup(Target = nameof(AddInvoicesArrayTest))]
        public void TruncateInvoicesTable()
        {
            // Delete all rows in Invoices table after each iteration
            this.ExecuteNonQuery("TRUNCATE TABLE Invoices");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            // Delete the database
            this.Dispose();
        }
    }
}