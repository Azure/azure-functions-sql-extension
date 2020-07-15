/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */
package com.microsoft.azure.functions.sql.annotation;

import com.microsoft.azure.functions.annotation.CustomBinding;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * <p>Annotation for SQL input bindings</p>
 */
@Target(ElementType.PARAMETER)
@Retention(RetentionPolicy.RUNTIME)
@CustomBinding(direction = "in", name = "sqlInput", type = "sql")
public @interface SQLInput {

    /**
     * Gets or sets the Connection String used to connect to the user's db
     */
    String connectionString();

    /**
     * Gets or sets the query that will be executed against the user's db
     */
    String sqlQuery();

    /**
     * Gets or sets the name of the Function app setting that will be used to
     * retreive the user's authentication info to connect to their db
     */
    String authentication();
}