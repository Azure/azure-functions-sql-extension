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
            using (var bulk = new SqlBulkCopy(connection))
            {
                bulk.DestinationTableName = "dbo.Products";
                bulk.WriteToServer(dataTable);
            }
            connection.Close();

            return new CreatedResult($"/api/addproduct", newProducts);
        } **/

        /**
        [FunctionName("AddProduct")]
        public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")] HttpRequest req,
        [SQLBinding(SQLQuery = "dbo.Products",
                    Authentication = "%SQLServerAuthentication%",
                    ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
        IAsyncCollector<Product> products)
        {
            var newProducts = GetNewProducts(10000);
            foreach (var product in newProducts)
            {
                products.AddAsync(product);
            }
            return new CreatedResult($"/api/addproduct", "done");
        } **/


        public static List<Product> GetNewProducts(int num)
        {
            var products = new List<Product>();
            for (int i = 1; i < num; i++)
            {
                var product = new Product
                {
                    ProductID = i,
                    Cost = 100,
                    Name = "test"
                };
                products.Add(product);
            }
            return products;
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
        [SQLBinding(SQLQuery = "dbo.Products",
                Authentication = "%SQLServerAuthentication%",
                ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
        out Product[] output)
        {
            output = new Product[2];
            var product = new Product();
            product.ProductID = 10;
            product.Name = "Bottle";
            product.Cost = 52;
            output[0] = product;
            product = new Product();
            product.ProductID = 11;
            product.Name = "Glasses";
            product.Cost = 12;
            output[1] = product;
            return new CreatedResult($"/api/products/10", product);
        } **/

    }
}
