/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.microsoft.azure.functions.sql.annotation;

import java.lang.annotation.Retention;
import java.lang.annotation.Target;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.ElementType;

import com.microsoft.azure.functions.annotation.CustomBinding;

@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.PARAMETER)
@CustomBinding(direction = "in", name = "", type = "sql")
public @interface SQLInput {
    /**
     * The variable name used in function.json.
     */
    String name();

    /**
     * Query string or name of stored procedure to be run.
     */
    String commandText();

    /**
     * Text or Stored Procedure.
     */
    CommandType commandType();

    /**
     * Parameters to the query or stored procedure. This string must follow the format
     * "@param1=param1,@param2=param2" where @param1 is the name of the parameter and
     * param1 is the parameter value.
     */
    String parameters() default "";

    /**
     * Setting name for SQL connection string.
     */
    String connectionStringSetting();
}
