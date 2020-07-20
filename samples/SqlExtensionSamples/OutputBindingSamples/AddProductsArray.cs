using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    public static class AddProductsArray
    {
        [FunctionName("AddProductsArray")]
        public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-array")]
            HttpRequest req,
        [Sql("Products", ConnectionStringSetting = "SQLServerAuthentication")] out Product[] output)
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
            return new CreatedResult($"/api/addproducts-array", output);
        }
    }
}
