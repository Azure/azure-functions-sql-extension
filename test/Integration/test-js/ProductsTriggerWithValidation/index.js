// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

module.exports = async function (context, changes) {
    const expectedMaxBatchSize = process.env["TEST_EXPECTED_MAX_BATCH_SIZE"]
    if (expectedMaxBatchSize && expectedMaxBatchSize != changes.length) {
        throw new Error(`Invalid max batch size, got ${changes.length} changes but expected ${expectedMaxBatchSize}`)
    }
    context.log(`SQL Changes: ${JSON.stringify(changes)}`)
}