package com.function.Common;

import java.util.Date;

public class ProductColumnTypes {
    private int ProductID;
    private Date Datetime;
    private Date Datetime2;

    public ProductColumnTypes(int productId, Date datetime, Date datetime2) {
        ProductID = productId;
        Datetime = datetime;
        Datetime2 = datetime2;
    }

    public int getProductId() {
        return ProductID;
    }

    public Date getDatetime() {
        return Datetime;
    }

    public Date getDatetime2() {
        return Datetime2;
    }
}
