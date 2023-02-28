// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

/**
 * This function is used to test compatability with converting various data types to their respective
 * SQL server types.
 */
module.exports = async function (context, req) {
    const product = {
        "ProductId": req.query.productId,
        "BigInt": 999,
        "Bit": true,
        "DecimalType": 1.2345,
        "Money": 1.2345,
        "Numeric": 1.2345,
        "SmallInt": 1,
        "SmallMoney": 1.2345,
        "TinyInt": 1,
        "FloatType": 0.1,
        "Real": 0.1,
        "Date": new Date().toISOString(),
        "Datetime": new Date().toISOString(),
        "Datetime2": new Date().toISOString(),
        "DatetimeOffset": new Date().toISOString(),
        "SmallDatetime": new Date().toISOString(),
        "Time": new Date().toISOString(),
        "CharType": "test",
        "Varchar": "test",
        "Nchar": "\u2649",
        "Nvarchar": "\u2649",
        "Binary": "dGVzdA==",
        "Varbinary": "dGVzdA=="
    };

    context.bindings.product = JSON.stringify(product);

    return {
        status: 201,
        body: product
    };
}