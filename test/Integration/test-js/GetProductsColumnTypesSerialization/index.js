// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

/**
 * This function verifies that serializing an item with various data types
 * works as expected.
 */
module.exports = async function (context, req, products) {
    context.log(JSON.stringify(products));
    return {
        status: 200,
        body: products
    };
}