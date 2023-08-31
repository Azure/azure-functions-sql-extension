/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

public class SqlChangeProduct {
    private SqlChangeOperation Operation;
    private Product Item;

    public SqlChangeProduct() {
    }

    public SqlChangeProduct(SqlChangeOperation operation, Product item) {
        this.Operation = operation;
        this.Item = item;
    }

    public SqlChangeOperation getOperation() {
        return Operation;
    }

    public void setOperation(SqlChangeOperation operation) {
        this.Operation = operation;
    }

    public Product getItem() {
        return Item;
    }

    public void setItem(Product item) {
        this.Item = item;
    }
}