using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.SQL;

namespace SQLBindingFunction
{
    
    class SQLBindingFunction
    {

        [FunctionName("GetProducts")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "products/{id}")]
            HttpRequest req,
            ILogger logger,
            [SQLBinding(SQLQuery = "select * from dbo.Products where ProductID = {id}",
                Authentication = "%SQLServerAuthentication%",
                ConnectionString = "Data Source=sotevo.database.windows.net;Database=TestDB;")]
            string attributes)
        {
            return (ActionResult)new OkObjectResult(attributes);
        }
    }
}
