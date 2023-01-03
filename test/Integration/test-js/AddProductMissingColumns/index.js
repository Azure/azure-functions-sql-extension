// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This output binding should successfully add the productMissingColumns object
// to the SQL table.
module.exports = async function (context) {
    const productMissingColumns = [{
        "ProductId": 1,
        "Name": "MissingCost"
    }];

    context.bindings.products = JSON.stringify(productMissingColumns);

    return {
        status: 201,
        body: productMissingColumns
    };
}