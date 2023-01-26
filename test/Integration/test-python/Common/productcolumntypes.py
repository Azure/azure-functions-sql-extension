# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import collections

class ProductColumnTypes(collections.UserDict):
    def __init__(self, productId, bigInt, bit, decimalType, money, numeric, smallInt,
        smallMoney, tinyInt, floatType, real, date, datetime, datetime2, datetimeOffset,
        smallDatetime, time, charType, varchar, nchar, nvarchar, binary, varBinary):
        super().__init__()
        self['ProductId'] = productId
        self['BigInt'] = bigInt
        self['Bit'] = bit
        self['DecimalType'] = decimalType
        self['Money'] = money
        self['Numeric'] = numeric
        self['SmallInt'] = smallInt
        self['SmallMoney'] = smallMoney
        self['TinyInt'] = tinyInt
        self['FloatType'] = floatType
        self['Real'] = real
        self['Date'] = date
        self['Datetime'] = datetime
        self['Datetime2'] = datetime2
        self['DatetimeOffset'] = datetimeOffset
        self['SmallDatetime'] = smallDatetime
        self['Time'] = time
        self['CharType'] = charType
        self['Varchar'] = varchar
        self['Nchar'] = nchar
        self['Nvarchar'] = nvarchar
        self['Binary'] = binary
        self['Varbinary'] = varBinary

