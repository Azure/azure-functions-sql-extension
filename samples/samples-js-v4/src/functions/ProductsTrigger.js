// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

const { app } = require('@azure/functions');

app.sql('ProductsTrigger', {
    connectionStringSetting: 'SqlConnectionString',
    tableName: 'dbo.Products',
    handler: async (changes, context) => {
        context.info(`SQL Changes: ${JSON.stringify(changes)}`);
    }
})
