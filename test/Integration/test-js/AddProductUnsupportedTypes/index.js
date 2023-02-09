// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This output binding should throw an exception because the target table has unsupported column types.
module.exports = async function (context, req) {
    context.bindings.product = {
        ProductId: 0,
        TextCol: "test",
        NtextCol: "test",
        ImageCol: "dGVzdA=="
    }

    return {
        status: 201,
        body: req.body
    };
}