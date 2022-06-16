// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

/// This timer function runs every 5 seconds, each time it upserts 1000 rows of data.
let executionNumber = 0;
module.exports = async function (context) {
    let totalUpserts = 1000;
    context.log(`{DateTime.Now} starting execution #{executionNumber}. Rows to generate={totalUpserts}.`);

    const start = Date.now();

    let products = [];
    for (let i = 0; i < totalUpserts; i++) {
        products.push({
            productId: i,
            name: "test",
            cost: 100 * i
        });
    }
    const duration = Date.now() - start;
    context.bindings.products = products;

    context.log(`{DateTime.Now} finished execution #{executionNumber}. Total time to create {totalUpserts} rows={duration}.`);

    executionNumber++;
};