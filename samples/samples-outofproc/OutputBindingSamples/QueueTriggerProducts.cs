// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extension.Sql;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.OutputBindingSamples
{
    public static class QueueTriggerProducts
    {
        [Function("QueueTriggerProducts")]
        [SqlOutput("[dbo].[Products]", ConnectionStringSetting = "SqlConnectionString")]
        public static List<Product> Run([QueueTrigger("testqueue")] string queueMessage)
        {
            int totalUpserts = 100;
            var products = new List<Product>();
            List<Product> newProducts = ProductUtilities.GetNewProducts(totalUpserts);
            foreach (Product product in newProducts)
            {
                products.Add(product);
            }

            return newProducts;
        }

    }
    //private static readonly Action<ILogger, DateTime, string, int, Exception> _queryTriggerRequested = LoggerMessage.Define(
    //        LogLevel.Information, new EventId(1, nameof(QueueTriggerProducts)), $"[QueueTrigger]: {Now} starting execution {QueueMessage}. Rows to generate={TotalUpserts}.");

}
