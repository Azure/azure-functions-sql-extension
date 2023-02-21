// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// If the `{name}` specified in the `getproducts-namenull/{name}` URL is "null",
// the query returns all rows for which the Name column is `NULL`. Otherwise, it returns
// all rows for which the value of the Name column matches the string passed in `{name}`
module.exports = async function (context, req, products) {
    context.log('JavaScript HTTP trigger function processed a request.');
    context.log(JSON.stringify(products));
    return {
        status: 200,
        body: products
    };
}