using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Data.SqlClient;
using System;

namespace SqlExtensionSamples
{
    public static class GetProductsSqlCommand
    {
      
        [FunctionName("GetProductsSqlCommand")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-sqlcommand/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SQLServerAuthentication")]
            SqlCommand command)
        {
            string result = string.Empty;
            using (SqlConnection connection = command.Connection)
            {
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result += String.Format("ProductID: {0}, Cost: {1}, Name: {2}\n", reader[0], reader[1], reader[2]);
                    }
                }
            }
            return (ActionResult)new OkObjectResult(result);
        }
    }
}
