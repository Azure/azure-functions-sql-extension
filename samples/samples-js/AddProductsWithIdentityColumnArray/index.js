// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

module.exports = async function (context, req) {
    context.log('JavaScript HTTP trigger function processed a request.');

    let products = [{
        name: "Cup",
        cost: "2"
    }, {
        name: "Glasses",
        cost: "12"
    }]
    context.bindings.products = products;

    return {
        status: 201,
        body: context.bindings.products
    };
}