// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#load "../Common/Product.csx"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;

public const int UpsertBatchSize = 1000;
public static ICollector<Product> Run(HttpRequest req, ILogger log, [Sql("dbo.ProductsNameNotNull", "SqlConnectionString")] ICollector<Product> products)
{
    List<Product> newProducts = ProductUtilities.GetNewProducts(UpsertBatchSize);
    foreach (Product product in newProducts)
    {
        products.Add(product);
    }

    var invalidProduct = new Product
    {
        Name = null,
        ProductId = UpsertBatchSize,
        Cost = 100
    };
    products.Add(invalidProduct);

    return products;
}
