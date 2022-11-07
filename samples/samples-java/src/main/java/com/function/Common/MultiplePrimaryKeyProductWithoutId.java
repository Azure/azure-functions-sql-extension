package com.function.Common;

public class MultiplePrimaryKeyProductWithoutId {
    private int ExternalId;
    private String Name;
    private int Cost;

    public MultiplePrimaryKeyProductWithoutId(int externalId, String name, int cost) {
        ExternalId = externalId;
        Name = name;
        Cost = cost;
    }

    public int getExternalId() {
        return ExternalId;
    }

    public String getName() {
        return Name;
    }

    public void setName(String name) {
        this.Name = name;
    }

    public int getCost() {
        return Cost;
    }

    public void setCost(int cost) {
        this.Cost = cost;
    }
}
