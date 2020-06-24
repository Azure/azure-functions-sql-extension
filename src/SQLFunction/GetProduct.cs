using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SQLFunction
{

    class GetProduct
    {

        [FunctionName("GetProduct")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "products/{id}")]
            HttpRequest req,
            ILogger logger,
            [SQLBinding(SQLQuery = "select * from dbo.Products",
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
        }

        public class Product
        {
            public int ProductID { get; set; }

            public string Name { get; set; }

            public int Cost { get; set; }
        }
    }
}
