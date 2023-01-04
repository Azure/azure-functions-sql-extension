// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This output binding should throw an error since the ProductsNameNotNull table does not 
// allows rows without a Name value. No rows should be upserted to the Sql table.
module.exports = async function (context) {
    var products = [];
    for(let i = 0; i < 1000; i++) {
        products.add({"ProductId": i,
        "Name": "test",
        "Cost": 100 * i});
    }
    const invalidProduct = {
        "Productid": 1000,
        "Name": null,
        "Cost": 1000
    };

    products.add(invalidProduct);

    context.bindings.products = JSON.stringify(products);

    return {
        status: 201,
        body: products
    };
}