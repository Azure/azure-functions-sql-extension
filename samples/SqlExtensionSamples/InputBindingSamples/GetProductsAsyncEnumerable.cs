using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    public static class GetProductsAsyncEnumerable
    {
        [FunctionName("GetProductsAsyncEnumerable")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-async/{cost}")]
             HttpRequest req,
            [Sql("select * from Products where cost = @Cost",
                 CommandType = System.Data.CommandType.Text,
                 Parameters = "@Cost={cost}",
                 ConnectionStringSetting = "SQLServerAuthentication")]
             IAsyncEnumerable<Product> products)
        {
            var enumerator = products.GetAsyncEnumerator();
            var productList = new List<Product>();
            while (await enumerator.MoveNextAsync())
            {
                productList.Add(enumerator.Current);
            }
            await enumerator.DisposeAsync();
            return (ActionResult)new OkObjectResult(productList);
        }
    }
}
