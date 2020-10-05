using System.Collections.Generic;

namespace SqlExtensionSamples
{
    public class ProductUtilities
    {
        public class Product
        {
            public int ProductID { get; set; }

            public string Name { get; set; }

            public int Cost { get; set; }

        }

        public static List<Product> GetNewProducts(int num)
        {
            var products = new List<Product>();
            for (int i = 0; i < num; i++)
            {
                var product = new Product
                {
                    ProductID = i,
                    Cost = 100,
                    Name = "test"
                };
                products.Add(product);
            }
            return products;
        }

        public static List<Product> GetNewProducts(int num, int cost)
        {
            var products = new List<Product>(num);
            for (int i = 0; i < num; i++)
            {
                var product = new Product
                {
                    ProductID = i,
                    Cost = cost,
                    Name = "test"
                };
                products.Add(product);
            }
            return products;
        }
    }
}