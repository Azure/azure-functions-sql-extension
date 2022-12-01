/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

import java.sql.Date;

public class ProductColumnTypes {
    private int ProductId;
    private Date Datetime;
    private Date Datetime2;

    public ProductColumnTypes(int productId, Date datetime, Date datetime2) {
        ProductId = productId;
        Datetime = datetime;
        Datetime2 = datetime2;
    }

    public int getProductId() {
        return ProductId;
    }

    public Date getDatetime() {
        return Datetime;
    }

    public void setDatetime(Date datetime) {
        Datetime = datetime;
    }

    public Date getDatetime2() {
        return Datetime2;
    }

    public void setDatetime2(Date datetime2) {
        Datetime2 = datetime2;
    }
}
