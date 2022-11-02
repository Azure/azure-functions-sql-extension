// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{
    public static class GetProductsSqlCommand
    {

        [Function("GetProductsSqlCommand")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-sqlcommand/{cost}")]
            HttpRequest req,
            [SqlInput("select * from Products where cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SqlConnectionString")]
            SqlCommand command)
        {
            string result = string.Empty;
            using (SqlConnection connection = command.Connection)
            {
                connection.Open();
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    result += $"ProductID: {reader["ProductID"]},  Name: {reader["Name"]}, Cost: {reader["Cost"]}\n";
                }
            }
            return new OkObjectResult(result);
        }
    }
}
