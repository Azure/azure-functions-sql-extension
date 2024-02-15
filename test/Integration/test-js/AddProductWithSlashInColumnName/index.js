// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This output binding should successfully add the productMissingColumns object
// to the SQL table.
module.exports = async function (context) {
    const productWithSlashInColumnName = [{
        "ProductId": 1,
        "Name/Test": "Test",
        "Cost\\Test": 1
    }];

    context.bindings.products = JSON.stringify(productWithSlashInColumnName);

    return {
        status: 201,
        body: productWithSlashInColumnName
    };
}