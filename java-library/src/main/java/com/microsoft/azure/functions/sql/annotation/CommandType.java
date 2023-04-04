/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */
package com.microsoft.azure.functions.sql.annotation;

/**
 * The type of commandText.
 */
public enum CommandType {
    /**
     * The commandText is a SQL query.
     */
    Text,

    /**
     * The commandText is a SQL stored procedure.
     */
    StoredProcedure
}
