// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This output binding should throw an exception because the target table has a column of type
// TEXT, which is not supported.
module.exports = async function (context, req) {
    context.bindings.product = req.body;

    return {
        status: 201,
        body: req.body
    };
}