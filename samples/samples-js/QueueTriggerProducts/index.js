// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

module.exports = async function (context, queueMessage) {
    let totalUpserts = 100;
    context.log(`[QueueTrigger]: {DateTime.Now} starting execution {queueMessage}. Rows to generate={totalUpserts}.`);

    const start = Date.now();

    let products = [];
    for (let i = 0; i < totalUpserts; i++) {
        products.push({
            productId: i,
            name: "test",
            cost: 100 * i
        });
    }
    context.bindings.products = products;
    const duration = Date.now() - start;

    context.log(`[QueueTrigger]: {DateTime.Now} finished execution {queueMessage}. Total time to create {totalUpserts} rows={duration}.`);
};