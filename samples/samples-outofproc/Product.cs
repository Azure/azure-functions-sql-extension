// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common
{
    public class Product
    {
        public int ProductId { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }

    public class ProductWithOptionalId
    {
        public int? ProductId { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }

    public class ProductName
    {
        public string Name { get; set; }
    }

    public class ProductWithoutId
    {
        public string Name { get; set; }

        public int Cost { get; set; }
    }
    public class ProductWithDefaultPK
    {
        public string Name { get; set; }

        public int Cost { get; set; }
    }
    public class ProductUtilities
    {
        /// <summary>
        /// Returns a list of <paramref name="num"/> Products with sequential IDs, a cost of 100, and "test" as name.
        /// </summary>
        public static List<Product> GetNewProducts(int num)
        {
            var products = new List<Product>();
            for (int i = 0; i < num; i++)
            {
                var product = new Product
                {
                    ProductId = i,
                    Cost = 100 * i,
                    Name = "test"
                };
                products.Add(product);
            }
            return products;
        }

        /// <summary>
        /// Returns a list of <paramref name="num"/> Products with a random cost between 1 and <paramref name="cost"/>.
        /// Note that ProductId is randomized too so list may not be unique.
        /// </summary>
        public static List<Product> GetNewProductsRandomized(int num, int cost)
        {
            var r = new Random();

            var products = new List<Product>(num);
            for (int i = 0; i < num; i++)
            {
                var product = new Product
                {
                    ProductId = r.Next(1, num),
                    Cost = (int)Math.Round(r.NextDouble() * cost),
                    Name = "test"
                };
                products.Add(product);
            }
            return products;
        }
    }
}