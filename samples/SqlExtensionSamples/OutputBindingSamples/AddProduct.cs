using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    
    public static class AddProduct
    {
        [FunctionName("AddProduct")]
        public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")]
            HttpRequest req,
        [Sql("Products", ConnectionStringSetting = "SQLServerAuthentication")] out Product product)
        {
            product = new Product
            {
                Name = req.Query["name"],
                ProductID = int.Parse(req.Query["id"]),
                Cost = int.Parse(req.Query["cost"])
            };
            return new CreatedResult($"/api/addproduct", product);
        }
    }
}
