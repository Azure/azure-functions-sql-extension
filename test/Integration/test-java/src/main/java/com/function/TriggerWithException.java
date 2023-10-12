/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function;

import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.sql.annotation.SQLTrigger;
import com.function.Common.SqlChangeProduct;
import com.google.gson.Gson;
import java.util.logging.Level;

public class TriggerWithException {
    public final String ExceptionMessage = "TriggerWithException test exception";
    private static Boolean threwException = false;

    @FunctionName("TriggerWithException")
    public void run(
            @SQLTrigger(name = "changes", tableName = "[dbo].[Products]", connectionStringSetting = "SqlConnectionString") SqlChangeProduct[] changes,
            ExecutionContext context) throws Exception {

        if (!threwException) {
            threwException = true;
            throw new Exception(ExceptionMessage);
        }
        context.getLogger().log(Level.INFO, "SQL Changes: " + new Gson().toJson(changes));
    }
}
