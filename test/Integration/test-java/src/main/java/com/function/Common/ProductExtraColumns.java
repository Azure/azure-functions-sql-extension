package com.function.Common;

public class ProductExtraColumns {
    private int ProductID;
    private String Name;
    private int Cost;
    private int ExtraInt;
    private String ExtraString;

    public ProductExtraColumns(int productId, String name, int cost, int extraInt, String extraString) {
        ProductID = productId;
        Name = name;
        Cost = cost;
        ExtraInt = extraInt;
        ExtraString = extraString;
    }

    public int getProductId() {
        return ProductID;
    }

    public String getName() {
        return Name;
    }

    public int getCost() {
        return Cost;
    }

    public int getExtraInt() {
        return ExtraInt;
    }

    public String getExtraString() {
        return ExtraString;
    }
}
