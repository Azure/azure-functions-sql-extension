/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

package com.function.Common;

import java.math.BigDecimal;

public class ProductColumnTypes {
    private int ProductId;
    private long BigInt;
    private boolean Bit;
    private BigDecimal DecimalType;
    private BigDecimal Money;
    private BigDecimal Numeric;
    private short SmallInt;
    private BigDecimal SmallMoney;
    private short TinyInt;
    private double FloatType;
    private double Real;
    private String Date;
    private String Datetime;
    private String Datetime2;
    private String DatetimeOffset;
    private String SmallDatetime;
    private String Time;
    private String CharType;
    private String Varchar;
    private String Nchar;
    private String Nvarchar;
    private String Binary;
    private String Varbinary;


    public ProductColumnTypes(int productId, long bigInt, boolean bit, BigDecimal decimalType, BigDecimal money,
    BigDecimal numeric, short smallInt, BigDecimal smallMoney, short tinyInt, double floatType, double real, String date,
    String datetime, String datetime2, String datetimeOffset, String smallDatetime, String time, String charType,
    String varchar, String nchar, String nvarchar, String binary, String varbinary) {
        ProductId = productId;
        BigInt = bigInt;
        Bit = bit;
        DecimalType = decimalType;
        Money = money;
        Numeric = numeric;
        SmallInt = smallInt;
        SmallMoney = smallMoney;
        TinyInt = tinyInt;
        FloatType = floatType;
        Real = real;
        Date = date;
        Datetime = datetime;
        Datetime2 = datetime2;
        DatetimeOffset = datetimeOffset;
        SmallDatetime = smallDatetime;
        Time = time;
        CharType = charType;
        Varchar = varchar;
        Nchar = nchar;
        Nvarchar = nvarchar;
        Binary = binary;
        Varbinary = varbinary;
    }

    public int getProductId() {
        return ProductId;
    }

    public long getBigint() {
        return BigInt;
    }

    public void setBigint(long bigInt) {
        BigInt = bigInt;
    }

    public boolean getBit() {
        return Bit;
    }

    public void setBit(boolean bit) {
        Bit = bit;
    }

    public BigDecimal getDecimalType() {
        return DecimalType;
    }

    public void setDecimalType(BigDecimal decimalType) {
        DecimalType = decimalType;
    }

    public BigDecimal getMoney() {
        return Money;
    }

    public void setMoney(BigDecimal money) {
        Money = money;
    }

    public BigDecimal getNumeric() {
        return Numeric;
    }

    public void setNumeric(BigDecimal numeric) {
        Numeric = numeric;
    }

    public short getSmallInt() {
        return SmallInt;
    }

    public void setSmallInt(short smallInt) {
        SmallInt = smallInt;
    }

    public BigDecimal getSmallMoney() {
        return SmallMoney;
    }

    public void setSmallMoney(BigDecimal smallMoney) {
        SmallMoney = smallMoney;
    }

    public short getTinyInt() {
        return TinyInt;
    }

    public void setTinyInt(short tinyInt) {
        TinyInt = tinyInt;
    }

    public double getFloatType() {
        return FloatType;
    }

    public void setFloatType(double floatType) {
        FloatType = floatType;
    }

    public double getReal() {
        return Real;
    }

    public void setReal(double real) {
        Real = real;
    }

    public String getDate() {
        return Date;
    }

    public void setDate(String date) {
        Date = date;
    }

    public String getDatetime() {
        return Datetime;
    }

    public void setDatetime(String datetime) {
        Datetime = datetime;
    }

    public String getDatetime2() {
        return Datetime2;
    }

    public void setDatetime2(String datetime2) {
        Datetime2 = datetime2;
    }

    public String getDatetimeOffset() {
        return DatetimeOffset;
    }

    public void setDatetimeOffset(String datetimeOffset) {
        DatetimeOffset = datetimeOffset;
    }

    public String getSmallDatetime() {
        return SmallDatetime;
    }

    public void setSmallDatetime(String smallDatetime) {
        SmallDatetime = smallDatetime;
    }

    public String getTime() {
        return Time;
    }

    public void setTime(String time) {
        Time = time;
    }

    public String getCharType() {
        return CharType;
    }

    public void setCharType(String charType) {
        CharType = charType;
    }

    public String getVarchar() {
        return Varchar;
    }

    public void setVarchar(String varchar) {
        Varchar = varchar;
    }

    public String getNchar() {
        return Nchar;
    }

    public void setNchar(String nchar) {
        Nchar = nchar;
    }

    public String getNvarchar() {
        return Nvarchar;
    }

    public void setNvarchar(String nvarchar) {
        Nvarchar = nvarchar;
    }

    public String getBinary() {
        return Binary;
    }

    public void setBinary(String binary) {
        Binary = binary;
    }

    public String getVarbinary() {
        return Varbinary;
    }

    public void setVarbinary(String varbinary) {
        Varbinary = varbinary;
    }
}