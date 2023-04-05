/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function;

import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.sql.annotation.SQLTrigger;

import java.util.logging.Level;

public class ProductsTrigger {
    @FunctionName("ProductsTrigger")
    public void run(
            @SQLTrigger(
                name = "changes",
                tableName = "[dbo].[Products]",
                connectionStringSetting = "SqlConnectionString")
                String changes,
            ExecutionContext context) {

        context.getLogger().log(Level.INFO, "SQL Changes: " + changes);
    }
}