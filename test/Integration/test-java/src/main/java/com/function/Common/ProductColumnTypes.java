/**
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for
 * license information.
 */

 package com.function.Common;

import java.math.BigDecimal;
import java.sql.Timestamp;

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
    private Timestamp Date;
    private Timestamp Datetime;
    private Timestamp Datetime2;
    private Timestamp DatetimeOffset;
    private Timestamp SmallDatetime;
    private String Time;
    private String CharType;
    private String Varchar;
    private String Nchar;
    private String Nvarchar;
    // private String UniqueIdentifier;


    public ProductColumnTypes(int productId, long bigInt, boolean bit, BigDecimal decimalType, BigDecimal money,
    BigDecimal numeric, short smallInt, BigDecimal smallMoney, short tinyInt, double floatType, double real, Timestamp date,
    Timestamp datetime, Timestamp datetime2, Timestamp datetimeOffset, Timestamp smallDatetime, String time, String charType,
    String varchar, String nchar, String nvarchar) {
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

    public boolean setBit() {
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

    public Timestamp getDate() {
        return Date;
    }

    public void setDate(Timestamp date) {
        Date = date;
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

    public Timestamp getDatetimeOffset() {
        return DatetimeOffset;
    }

    public void setDatetimeOffset(Timestamp datetimeOffset) {
        DatetimeOffset = datetimeOffset;
    }

    public Timestamp getSmallDatetime() {
        return SmallDatetime;
    }

    public void setSmallDatetime(Timestamp smallDatetime) {
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
}