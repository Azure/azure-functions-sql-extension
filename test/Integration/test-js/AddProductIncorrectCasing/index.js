// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

/**
 * This output binding should throw an error since the casing of the POCO field 'ProductID'
 * and table column name 'ProductId' do not match.
 */
module.exports = async function (context, req) {
    const product = {
        "ProductID": req.query.productId,
        "Datetime": new Date().toISOString(),
        "Datetime2": new Date().toISOString()
    };

    context.bindings.product = JSON.stringify(product);

    return {
        status: 201,
        body: product
    };
}