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


public class TableNotPresentTrigger {
    @FunctionName("TableNotPresentTrigger")
    public void run(
            @SQLTrigger(
                name = "changes",
                tableName = "[dbo].[TableNotPresent]",
                connectionStringSetting = "SqlConnectionString")
                SqlChangeProduct[] changes,
            ExecutionContext context) {

        throw new RuntimeException("Associated test case should fail before the function is invoked.");
    }
}