/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

import com.fasterxml.jackson.annotation.JsonProperty;

public class ProductUnsupportedTypes {
    @JsonProperty("ProductId")
    private int ProductId;
    @JsonProperty("Text")
    private String Text;

    public ProductUnsupportedTypes() {
    }

    public ProductUnsupportedTypes(int productId, String text) {
        ProductId = productId;
        Text = text;
    }

    public int getProductId() {
        return ProductId;
    }

    public void setProductId(int productId) {
        ProductId = productId;
    }

    public String getText() {
        return Text;
    }

    public void setText(String text) {
        Text = text;
    }
}