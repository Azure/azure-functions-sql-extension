// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlTriggerBindingIntegrationTests : IntegrationTestBase
    {
        public SqlTriggerBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Tests for insertion of products triggering the function.
        /// </summary>
        [Fact]
        public async void InsertProductsTest()
        {
            int countInsert = 0;
            this.EnableChangeTracking();
            this.StartFunctionHost(nameof(ProductsTrigger), Common.SupportedLanguages.CSharp);
            this.FunctionHost.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e != null && !string.IsNullOrEmpty(e.Data) && e.Data.Contains($"Change occurred to Products table row"))
                {
                    countInsert++;
                }
            };

            Product[] products = GetProducts(3, 100);
            this.InsertProducts(products);
            await Task.Delay(5000);

            Assert.Equal(3, countInsert);

        }

        /// <summary>
        /// Tests insertion into table with multiple primary key columns.
        /// </summary>
        [Fact]
        public void InsertMultiplePrimaryKeyColumnsTest()
        {
            this.EnableChangeTracking();
            this.StartFunctionHost(nameof(ProductsWithMultiplePrimaryColumnsTrigger), Common.SupportedLanguages.CSharp);
            var taskCompletionSource = new TaskCompletionSource<bool>();
            this.FunctionHost.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e != null && !string.IsNullOrEmpty(e.Data) && e.Data.Contains($"Change occurred to ProductsWithMultiplePrimaryColumns table row"))
                {
                    taskCompletionSource.SetResult(true);
                }
            };

            string query = $@"INSERT INTO dbo.ProductsWithMultiplePrimaryColumnsAndIdentity VALUES(123, 'ProductTest', 100);";
            this.ExecuteNonQuery(query);
            taskCompletionSource.Task.Wait(10000);

            Assert.True(taskCompletionSource.Task.Result);

        }

        /// <summary>
        /// Tests for behaviour of the trigger when insertion, updates, and deletes occur.
        /// </summary>
        [Fact]
        public async void InsertUpdateDeleteProductsTest()
        {
            int countInsert = 0;
            int countUpdate = 0;
            int countDelete = 0;
            this.EnableChangeTracking();
            this.StartFunctionHost(nameof(ProductsTrigger), Common.SupportedLanguages.CSharp);
            this.FunctionHost.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e != null && !string.IsNullOrEmpty(e.Data) && e.Data.Contains($"Change occurred to Products table row: Insert"))
                {
                    countInsert++;
                }
                if (e != null && !string.IsNullOrEmpty(e.Data) && e.Data.Contains($"Change occurred to Products table row: Update"))
                {
                    countUpdate++;
                }
                if (e != null && !string.IsNullOrEmpty(e.Data) && e.Data.Contains($"Change occurred to Products table row: Delete"))
                {
                    countDelete++;
                }
            };
            Product[] products = GetProducts(3, 100);
            this.InsertProducts(products);
            await Task.Delay(500);
            this.UpdateProducts(products.Take(2).ToArray());
            await Task.Delay(500);
            this.DeleteProducts(products.Take(1).ToArray());

            await Task.Delay(5000);

            //Since insert and update counts as a single insert and insert and delete counts as a single delete
            Assert.Equal(2, countInsert);
            Assert.Equal(0, countUpdate);
            Assert.Equal(1, countDelete);

        }

        private static Product[] GetProducts(int n, int cost)
        {
            var result = new Product[n];
            for (int i = 1; i <= n; i++)
            {
                result[i - 1] = new Product
                {
                    ProductID = i,
                    Name = "test",
                    Cost = cost * i
                };
            }
            return result;
        }
        private void InsertProducts(Product[] products)
        {
            if (products.Length == 0)
            {
                return;
            }

            var queryBuilder = new StringBuilder();
            foreach (Product p in products)
            {
                queryBuilder.AppendLine($"INSERT INTO dbo.Products VALUES({p.ProductID}, '{p.Name}', {p.Cost});");
            }

            this.ExecuteNonQuery(queryBuilder.ToString());
        }
        private void UpdateProducts(Product[] products)
        {
            if (products.Length == 0)
            {
                return;
            }

            var queryBuilder = new StringBuilder();
            foreach (Product p in products)
            {
                string newName = p.Name + "Update";
                queryBuilder.AppendLine($"UPDATE dbo.Products set Name = '{newName}' where ProductId = {p.ProductID};");
            }

            this.ExecuteNonQuery(queryBuilder.ToString());
        }
        private void DeleteProducts(Product[] products)
        {
            if (products.Length == 0)
            {
                return;
            }

            var queryBuilder = new StringBuilder();
            foreach (Product p in products)
            {
                queryBuilder.AppendLine($"DELETE from dbo.Products where ProductId = {p.ProductID};");
            }

            this.ExecuteNonQuery(queryBuilder.ToString());
        }
    }
}