// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#r "Newtonsoft.Json"

using System;
using System.Linq;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

public class Product
{
    public int ProductId { get; set; }

    public string Name { get; set; }

    public int Cost { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is Product)
        {
            var that = obj as Product;
            return this.ProductId == that.ProductId && this.Name == that.Name && this.Cost == that.Cost;
        }
        return false;
    }
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

    public override bool Equals(object obj)
    {
        if (obj is Product)
        {
            var that = obj as Product;
            return this.Name == that.Name;
        }
        return false;
    }
}

public class ProductWithDefaultPK
{
    public string Name { get; set; }

    public int Cost { get; set; }
}
public class ProductWithoutId
{
    public string Name { get; set; }

    public int Cost { get; set; }
}
public class MultiplePrimaryKeyProductWithoutId
{
    public int ExternalId { get; set; }

    public string Name { get; set; }

    public int Cost { get; set; }
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

    public double FloatType { get; set; }

    public float Real { get; set; }

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

    public override string ToString()
    {
        return $"[{this.ProductId}, {this.BigInt}, {this.Bit}, {this.DecimalType}, {this.Money}, {this.Numeric}, {this.SmallInt}, {this.SmallMoney}, {this.TinyInt}, {this.FloatType}, {this.Real}, {this.Date}, {this.Datetime}, {this.Datetime2}, {this.DatetimeOffset}, {this.SmallDatetime}, {this.Time}, {this.CharType}, {this.Varchar}, {this.Nchar}, {this.Nvarchar}, {this.Binary}, {this.Varbinary}]";
    }
}

public class ProductDefaultPKAndDifferentColumnOrder
{
    public int Cost { get; set; }

    public string Name { get; set; }
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

public class ProductIncorrectCasing
{
    public int ProductID { get; set; }

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

public static class Utils
{
    /// <summary>
    /// Default JSON serializer settings to use
    /// </summary>
    private static readonly JsonSerializerSettings _defaultJsonSerializationSettings;

    static Utils()
    {
        _defaultJsonSerializationSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver()
        };
    }

    /// <summary>
    /// Serializes the specified object into a JSON string.
    /// </summary>
    /// <param name="obj">The object to serialize</param>
    /// <param name="settings">The specific settings to use, uses a simple set of default settings if not specified</param>
    /// <returns>The serialized JSON string</returns>
    /// <remarks>This will NOT use any global settings to avoid picking up changes that may have been made by other code running in the host (such as user functions)</remarks>
    public static string JsonSerializeObject(object obj, JsonSerializerSettings settings = null)
    {
        settings = settings ?? _defaultJsonSerializationSettings;
        // Following the Newtonsoft implementation in JsonConvert of creating a new JsonSerializer each time.
        // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L612
        // If performance ends up being an issue could look into creating a single instance of the serializer for each setting.
        var serializer = JsonSerializer.Create(settings);
        // 256 is value used by Newtonsoft by default - helps avoid having to expand it too many times for larger strings
        // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L659
        var sb = new StringBuilder(256);
        var sw = new StringWriter(sb);
        using (JsonWriter writer = new JsonTextWriter(sw))
        {
            serializer.Serialize(writer, obj);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Deserializes the JSON string into an instance of the specified type
    /// </summary>
    /// <typeparam name="T">The type to deserialize into</typeparam>
    /// <param name="json">The string containing the JSON</param>
    /// <param name="settings">The specific settings to use, uses a simple set of default settings if not specified</param>
    /// <returns>The instance of T being deserialized</returns>
    /// <remarks>This will NOT use any global settings to avoid picking up changes that may have been made by other code running in the host (such as user functions)</remarks>
    public static T JsonDeserializeObject<T>(string json, JsonSerializerSettings settings = null)
    {
        settings = settings ?? _defaultJsonSerializationSettings;
        // Following the Newtonsoft implementation in JsonConvert of creating a new JsonSerializer each time.
        // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L821
        // If performance ends up being an issue could look into creating a single instance of the serializer for each setting.
        var serializer = JsonSerializer.Create(settings);
        using (JsonReader reader = new JsonTextReader(new StringReader(json)))
        {
            return serializer.Deserialize<T>(reader);
        }
    }
}