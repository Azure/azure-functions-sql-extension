// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This shows an example of a SQL Output binding where the target table has a default primary key
// of type uniqueidentifier and the column is not included in the output object. A new row will
// be inserted and the uniqueidentifier will be generated by the engine.
module.exports = async function (context, req) {
    // Note that this expects the body to be a JSON object
    // matching each of the columns in the table to upsert to.
    context.bindings.products = req.body;

    return {
        status: 201,
        body: req.body
    };
}