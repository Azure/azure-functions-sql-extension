// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// The parameter value of the `@Name` parameter is an empty string in the parameters arguments.
module.exports = async function (context, req, products) {
    context.log('JavaScript HTTP trigger function processed a request.');
    context.log(JSON.stringify(products));
    return {
        status: 200,
        body: products
    };
}