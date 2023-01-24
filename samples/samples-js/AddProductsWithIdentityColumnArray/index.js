// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

/// This shows an example of a SQL Output binding where the target table has a primary key
/// which is an identity column. In such a case the primary key is not required to be in
/// the object used by the binding - it will insert a row with the other values and the
/// ID will be generated upon insert.
module.exports = async function (context, req) {
    let products = [{
        Name: "Cup",
        Cost: "2"
    }, {
        Name: "Glasses",
        Cost: "12"
    }]
    context.bindings.products = products;

    return {
        status: 201,
        body: context.bindings.products
    };
}