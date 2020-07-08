using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace SQLFunction
{

    class GetProduct
    {
       
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
            output.FlushAsync();
            return new CreatedResult($"/api/products/10", product);
        }

       /**
        [FunctionName("GetProducts")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "products/{cost}")]
            HttpRequest req,
            ILogger logger,
            [SQLBinding(SQLQuery = "select * from dbo.Products where cost = {cost}",
                Authentication = "%SQLServerAuthentication%",
                ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
            IEnumerable<Product> products)
        {
            string result = string.Empty;
            foreach (var product in products)
            {
                result += JsonConvert.SerializeObject(product) + "\n";
            }
            return (ActionResult)new OkObjectResult(result);
        } **/

        /**
        [FunctionName("GetProduct")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "products/{id}")]
            HttpRequest req,
            ILogger logger,
            [SQLBinding(SQLQuery = "select * from dbo.Products",
                Authentication = "%SQLServerAuthentication%",
                ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
            SqlCommand command)
        {
            string result = string.Empty;
            using (SqlConnection connection = command.Connection)
            {
                try
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result += reader[0];
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Exception in executing query: " + e.Message);
                }

            }
            return (ActionResult)new OkObjectResult(result);
        } **/


        public class Product
        {
            public int ProductID { get; set; }

            public string Name { get; set; }

            public int Cost { get; set; }
        }
    }
}
