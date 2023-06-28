/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

public class SqlChangeProductColumnTypes {
    private SqlChangeOperation Operation;
    private ProductColumnTypes Item;

    public SqlChangeProductColumnTypes() {
    }

    public SqlChangeProductColumnTypes(SqlChangeOperation operation, ProductColumnTypes item) {
        this.Operation = operation;
        this.Item = item;
    }

    public SqlChangeOperation getOperation() {
        return Operation;
    }

    public void setOperation(SqlChangeOperation operation) {
        this.Operation = operation;
    }

    public ProductColumnTypes getItem() {
        return Item;
    }

    public void setItem(ProductColumnTypes item) {
        this.Item = item;
    }
}