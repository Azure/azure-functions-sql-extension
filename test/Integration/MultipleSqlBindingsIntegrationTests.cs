// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.MultipleBindingsSamples;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection(IntegrationTestsCollection.Name)]
    [LogTestName]
    public class MultipleSqlBindingsIntegrationTests : IntegrationTestBase
    {
        public MultipleSqlBindingsIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Tests a function with both an input and output binding.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public async void GetAndAddProductsTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetAndAddProducts), lang);

            // Insert 10 rows to Products table
            Product[] products = GetProductsWithSameCost(10, 100);
            this.InsertProducts(products);

            // Run the function
            await this.SendInputRequest("getandaddproducts/100");

            // Verify that the 10 rows in Products were upserted to ProductsWithIdentity
            Assert.Equal(10, this.ExecuteScalar("SELECT COUNT(1) FROM ProductsWithIdentity"));
        }
    }
}
