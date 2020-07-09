using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using static SQLFunction.GetProducts;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Build.Framework;

namespace SQLFunction
{
    public static class AddProducts
    {
        [FunctionName("AddProduct")]
        public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")]
            HttpRequest req,
        [SQLBinding(SQLQuery = "dbo.Products",
                Authentication = "%SQLServerAuthentication%",
                ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
        out Product product)
        {
            product = new Product
            {
                Name = req.Query["name"],
                ProductID = int.Parse(req.Query["id"]),
                Cost = int.Parse(req.Query["cost"])
            };
            return new CreatedResult($"/api/addproduct", product);
        }

        /**
        [FunctionName("AddProduct")]
        public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")] HttpRequest req)
        {
            var newProducts = GetNewProducts();
            SqlConnection connection = BuildSqlConnection();
           
            string rows = JsonConvert.SerializeObject(newProducts);
            DataTable dataTable = (DataTable)JsonConvert.DeserializeObject(rows, typeof(DataTable));
            dataTable.TableName = "dbo.Products";
            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(dataTable);
            var dataAdapter = new SqlDataAdapter("SELECT * FROM dbo.Products;", connection);
            SqlCommandBuilder commandBuilder = new SqlCommandBuilder(dataAdapter);
            connection.Open();
            dataAdapter.Update(dataSet, "dbo.Products");
            connection.Close();

            return new CreatedResult($"/api/addproduct", newProducts);
        }


        [FunctionName("AddProduct")]
        public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")]
        [SQLBinding(SQLQuery = "dbo.Products",
                    Authentication = "%SQLServerAuthentication%",
                    ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
        IAsyncCollector<Product> products)
        {
            var newProducts = GetNewProducts();
            foreach (var product in newProducts)
            {
                products.AddAsync(product);
            }
            return new CreatedResult($"/api/addproduct", newProducts);
        } **/


        public static List<Product> GetNewProducts()
        {
            return null;
        }

        public static SqlConnection BuildSqlConnection()
        {
            return null;
        }

        /**

        [FunctionName("AddProduct")]
        public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")]
            HttpRequest req,
            ILogger logger,
        [SQLBinding(SQLQuery = "dbo.Products",
                Authentication = "%SQLServerAuthentication%",
                ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
        IAsyncCollector<Product> output)
        {
            var product = new Product();
            product.ProductID = 10;
            product.Name = "Bottle";
            product.Cost = 52;
            output.AddAsync(product);
            product = new Product();
            product.ProductID = 11;
            product.Name = "Glasses";
            product.Cost = 12;
            output.AddAsync(product);
            return new CreatedResult($"/api/products/10", product);
        } **/

    }
}
