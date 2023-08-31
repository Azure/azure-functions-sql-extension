/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function;

import com.function.Common.User;
import com.google.gson.Gson;
import com.microsoft.azure.functions.ExecutionContext;
import com.microsoft.azure.functions.annotation.FunctionName;
import com.microsoft.azure.functions.sql.annotation.SQLTrigger;

import java.util.logging.Level;

public class ReservedTableNameTrigger {
    @FunctionName("ReservedTableNameTrigger")
    public void run(
            @SQLTrigger(
                name = "changes",
                tableName = "[dbo].[User]",
                connectionStringSetting = "SqlConnectionString")
                User[] changes,
            ExecutionContext context) throws Exception {

        context.getLogger().log(Level.INFO, "SQL Changes: " + new Gson().toJson(changes));
    }
}