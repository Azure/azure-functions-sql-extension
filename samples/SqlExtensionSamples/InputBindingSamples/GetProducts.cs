using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Collections.Generic;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{

    public static class GetProducts
    {
        [FunctionName("GetProducts")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where Cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SQLServerAuthentication")]
            IEnumerable<Product> products)
        {
            return (ActionResult)new OkObjectResult(products);
        }
    }
}
