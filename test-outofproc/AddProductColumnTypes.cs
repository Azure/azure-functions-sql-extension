﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Web;
using System.Collections.Specialized;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using DotnetIsolatedTests.Common;
using System;
using System.Data.SqlTypes;

namespace DotnetIsolatedTests
{
    public static class AddProductColumnTypes
    {
        /// <summary>
        /// This function is used to test compatability with converting various data types to their respective
        /// SQL server types.
        /// </summary>
        [Function(nameof(AddProductColumnTypes))]
        [SqlOutput("dbo.ProductsColumnTypes", ConnectionStringSetting = "SqlConnectionString")]
        public static ProductColumnTypes Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-columntypes")] HttpRequestData req)
        {
            NameValueCollection queryStrings = HttpUtility.ParseQueryString(req.Url.Query);
            var product = new ProductColumnTypes()
            {
                ProductID = int.Parse(queryStrings["productId"], null),
                Datetime = new SqlDateTime(DateTime.UtcNow).Value,
                Datetime2 = DateTime.UtcNow
            };
            return product;
        }
    }
}
