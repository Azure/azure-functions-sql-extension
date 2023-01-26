// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
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

        public override string ToString()
        {
            return $"[{this.ProductId}, {this.BigInt}, {this.Bit}, {this.DecimalType}, {this.Money}, {this.Numeric}, {this.SmallInt}, {this.SmallMoney}, {this.TinyInt}, {this.FloatType}, {this.Real}, {this.Date}, {this.Datetime}, {this.Datetime2}, {this.DatetimeOffset}, {this.SmallDatetime}, {this.Time}, {this.CharType}, {this.Varchar}, {this.Nchar}, {this.Nvarchar}, {this.Binary}, {this.Varbinary}]";
        }
    }
}
