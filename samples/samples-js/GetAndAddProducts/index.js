// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

/**
 * This function uses a SQL input binding to get products from the Products table
 * and upsert those products to the ProductsWithIdentity table.
 */
module.exports = async function (context, req, products) {
    context.bindings.productsWithIdentity = products;

    return {
        status: 201,
        body: products
    };
}