﻿DROP TABLE IF EXISTS [ProductsColumnTypes];

CREATE TABLE [ProductsColumnTypes] (
    [ProductId] [int] NOT NULL PRIMARY KEY,
    [BigInt] [bigint],
    [Bit] [bit],
    [DecimalType] [decimal](18,4),
    [Money] [money],
    [Numeric] [numeric](18,4),
    [SmallInt] [smallint],
    [SmallMoney] [smallmoney],
    [TinyInt] [tinyint],
    [FloatType] [float],
    [Real] [real],
    [Date] [date],
    [Datetime] [datetime],
    [Datetime2] [datetime2],
    [DatetimeOffset] [datetimeoffset],
    [SmallDatetime] [smalldatetime],
    [Time] [time],
    [CharType] [char](4),
    [Varchar] [varchar](100),
    [Nchar] [nchar](4),
    [Nvarchar] [nvarchar](100),
    [Binary] [binary](4),
    [Varbinary] [varbinary](100)
)
