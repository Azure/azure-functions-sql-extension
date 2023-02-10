// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// `SelectsProductCost` is the name of a procedure stored in the user's database.
// In this case, *CommandType* is `StoredProcedure`. The parameter value of the `@Cost` parameter in the
// procedure is once again the `{cost}` specified in the `getproducts-storedprocedure/{cost}` URL.
module.exports = async function (context, req, products) {
    context.log('JavaScript HTTP trigger function processed a request.');
    context.log(JSON.stringify(products));
    return {
        status: 200,
        body: products
    };
}