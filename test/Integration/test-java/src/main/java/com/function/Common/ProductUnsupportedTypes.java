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
    @JsonProperty("TextCol")
    private String TextCol;
    @JsonProperty("NtextCol")
    private String NtextCol;
    @JsonProperty("ImageCol")
    private String ImageCol;

    public ProductUnsupportedTypes() {
    }

    public ProductUnsupportedTypes(int productId, String textCol, String ntextCol, String imageCol) {
        ProductId = productId;
        TextCol = textCol;
        NtextCol = ntextCol;
        ImageCol = imageCol;
    }

    public int getProductId() {
        return ProductId;
    }

    public void setProductId(int productId) {
        ProductId = productId;
    }

    public String getTextCol() {
        return TextCol;
    }

    public void setTextCol(String textCol) {
        TextCol = textCol;
    }

    public String getNtextCol() {
        return NtextCol;
    }

    public void setNtextCol(String ntextCol) {
        NtextCol = ntextCol;
    }

    public String getImageCol() {
        return ImageCol;
    }

    public void setImageCol(String imageCol) {
        ImageCol = imageCol;
    }
}