﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
                ConnectionStringSetting = "SqlConnectionString")]
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
                        result += $"ProductID: {reader[0]}, Cost: {reader[1]}, Name: {reader[2]}\n";
                    }
                }
            }
            return (ActionResult)new OkObjectResult(result);
        }
    }
}
