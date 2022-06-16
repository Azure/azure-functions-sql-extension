// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// Upsert the products, which will insert them into the Products table if the primary key (ProductId) for that item doesn't exist.
// If it does then update it to have the new name and cost.
module.exports = async function (context, req) {
    // Note that this expects the body to be a JSON object or array of objects which have a property
    // matching each of the columns in the table to upsert to.
    context.bindings.products = req.body;

    return {
        status: 201,
        body: req.body
    };
}