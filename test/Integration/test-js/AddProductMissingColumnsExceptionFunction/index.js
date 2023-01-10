// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This output binding should throw an error since the ProductsCostNotNull table does not
// allow rows without a Cost value.
module.exports = async function (context) {
    const products = [{
        "productId": 1,
        "name": "MissingCost"
    }];

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}