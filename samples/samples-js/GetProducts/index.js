// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// The input binding executes the `select * from Products where Cost = @Cost` query, returning the result as json object in the body.
// The *parameters* argument passes the `{cost}` specified in the URL that triggers the function,
// `getproducts/{cost}`, as the value of the `@Cost` parameter in the query.
// *commandType* is set to `Text`, since the constructor argument of the binding is a raw query.
module.exports = async function (context, req, products) {
    context.log('JavaScript HTTP trigger function processed a request.');
    context.log(JSON.stringify(products));
    return {
        status: 200,
        body: products
    };
}