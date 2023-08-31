/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function;

import com.function.Common.SqlChangeProduct;
import com.google.gson.Gson;
import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.sql.annotation.SQLTrigger;

import java.util.logging.Level;

public class ProductsTriggerWithValidation {
    @FunctionName("ProductsTriggerWithValidation")
    public void run(
            @SQLTrigger(
                name = "changes",
                tableName = "[dbo].[Products]",
                connectionStringSetting = "SqlConnectionString")
                SqlChangeProduct[] changes,
            ExecutionContext context) throws Exception {

        String expectedMaxBatchSize = System.getenv("TEST_EXPECTED_MAX_BATCH_SIZE");
        if (expectedMaxBatchSize != null && !expectedMaxBatchSize.isEmpty() && Integer.parseInt(expectedMaxBatchSize) != changes.length) {
            throw new Exception("Invalid max batch size, got " + changes.length + " changes but expected " + expectedMaxBatchSize);
        }
        context.getLogger().log(Level.INFO, "SQL Changes: " + new Gson().toJson(changes));
    }
}