using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace SqlExtensionSamples.InputBindingSamples
{
    public static class GetProductsString
    {
        [FunctionName("GetProductsString")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-string/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SQLServerAuthentication")]
            string products)
        {
            return (ActionResult)new OkObjectResult(products);
        }
    }
}
