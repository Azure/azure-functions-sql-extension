using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Collections.Generic;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    public static class GetProductsNameNull
    {
        // In this example, the value passed to the @Name parameter is null
        [FunctionName("GetProductsNameNull")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-namenull/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where Cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost},@Name=null",
                ConnectionStringSetting = "SQLServerAuthentication")]
            IEnumerable<Product> products)
        {
            return (ActionResult)new OkObjectResult(products);
        }
    }
}
