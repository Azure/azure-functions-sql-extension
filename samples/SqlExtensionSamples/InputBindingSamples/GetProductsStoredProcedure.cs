using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Collections.Generic;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{

    public static class GetProductsStoredProcedure
    {
        [FunctionName("GetProductsStoredProcedure")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-storedprocedure/{cost}")]
            HttpRequest req,
            [Sql("SelectProductsCost",
                CommandType = System.Data.CommandType.StoredProcedure,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SQLServerAuthentication")]
            IEnumerable<Product> products)
        {
            return (ActionResult)new OkObjectResult(products);
        }
    }
}