// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System.Collections.Generic;
using System;
using System.Linq;

namespace DotnetIsolatedTests.Common
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

    public class ProductIncorrectCasing
    {
        public int ProductID { get; set; }

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

    public class ProductColumnTypes
    {
        public int ProductId { get; set; }

        public long BigInt { get; set; }

        public bool Bit { get; set; }

        public decimal DecimalType { get; set; }

        public decimal Money { get; set; }

        public decimal Numeric { get; set; }

        public short SmallInt { get; set; }

        public decimal SmallMoney { get; set; }

        public short TinyInt { get; set; }

        public Double FloatType { get; set; }

        public Single Real { get; set; }

        public DateTime Date { get; set; }

        public DateTime Datetime { get; set; }

        public DateTime Datetime2 { get; set; }

        public DateTimeOffset DatetimeOffset { get; set; }

        public DateTime SmallDatetime { get; set; }

        public TimeSpan Time { get; set; }

        public string CharType { get; set; }

        public string Varchar { get; set; }

        public string Nchar { get; set; }

        public string Nvarchar { get; set; }

        public byte[] Binary { get; set; }

        public byte[] Varbinary { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is ProductColumnTypes)
            {
                var that = obj as ProductColumnTypes;
                return this.ProductId == that.ProductId && this.BigInt == that.BigInt && this.Bit == that.Bit &&
                    this.DecimalType == that.DecimalType && this.Money == that.Money && this.Numeric == that.Numeric &&
                    this.SmallInt == that.SmallInt && this.SmallMoney == that.SmallMoney && this.TinyInt == that.TinyInt &&
                    this.FloatType == that.FloatType && this.Real == that.Real && this.Date == that.Date &&
                    this.Datetime == that.Datetime && this.Datetime2 == that.Datetime2 && this.DatetimeOffset == that.DatetimeOffset &&
                    this.SmallDatetime == that.SmallDatetime && this.Time == that.Time && this.CharType == that.CharType &&
                    this.Varchar == that.Varchar && this.Nchar == that.Nchar && this.Nvarchar == that.Nvarchar &&
                    this.Binary.SequenceEqual(that.Binary) && this.Varbinary.SequenceEqual(that.Varbinary);
            }
            return false;
        }
    }

    public class ProductExtraColumns
    {
        public int ProductId { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }

        public int ExtraInt { get; set; }

        public string ExtraString { get; set; }
    }

    public class ProductIncludeIdentity
    {
        public int ProductId { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }
    public class ProductMissingColumns
    {
        public int ProductId { get; set; }

        public string Name { get; set; }
    }

    public class ProductUnsupportedTypes
    {
        public int ProductId { get; set; }

        public string TextCol { get; set; }

        public string NtextCol { get; set; }

        public byte[] ImageCol { get; set; }
    }
}