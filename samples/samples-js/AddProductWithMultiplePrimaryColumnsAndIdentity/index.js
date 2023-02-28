// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    const testProduct = {
        "ExternalId": req.query?.externalId,
        "Name": req.query?.name,
        "Cost": req.query?.cost
    };

    context.bindings.product = JSON.stringify(testProduct);

    return {
        status: 201,
        body: context.bindings.product
    };
}