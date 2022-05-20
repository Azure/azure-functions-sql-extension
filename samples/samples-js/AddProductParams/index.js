// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const newProduct = {
        "productId": req.query?.productId,
        "name": req.query?.name,
        "cost": req.query?.cost
    };

    context.bindings.product = JSON.stringify(newProduct);

    return {
        status: 201,
        body: context.bindings.product
    };
}