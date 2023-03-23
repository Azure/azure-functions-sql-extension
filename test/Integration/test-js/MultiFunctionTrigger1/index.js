// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

module.exports = async function (context, changes) {
    // logger.LogInformation("Trigger1 Changes: " + Utils.JsonSerializeObject(products));
    context.log(`Trigger1 Changes: ${JSON.stringify(changes)}`)
}