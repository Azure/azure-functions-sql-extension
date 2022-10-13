package com.function.Common;

public class ProductMissingColumns {
    private int ProductID;
    private String Name;

    public ProductMissingColumns(int productId, String name) {
        ProductID = productId;
        Name = name;
    }

    public int getProductId() {
        return ProductID;
    }

    public String getName() {
        return Name;
    }
}
