/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

import java.sql.Timestamp;

public class ProductColumnTypes {
    private int ProductId;
    private Timestamp Datetime;
    private Timestamp Datetime2;

    public ProductColumnTypes(int productId, Timestamp datetime, Timestamp datetime2) {
        ProductId = productId;
        Datetime = datetime;
        Datetime2 = datetime2;
    }

    public int getProductId() {
        return ProductId;
    }

    public Timestamp getDatetime() {
        return Datetime;
    }

    public void setDatetime(Timestamp datetime) {
        Datetime = datetime;
    }

    public Timestamp getDatetime2() {
        return Datetime2;
    }

    public void setDatetime2(Timestamp datetime2) {
        Datetime2 = datetime2;
    }
}
