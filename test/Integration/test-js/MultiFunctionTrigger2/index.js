// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

module.exports = async function (context, changes) {
    context.log(`Trigger2 Changes: ${JSON.stringify(changes)}`)
}