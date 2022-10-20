package com.function.Common;

// import java.sql.Date;
// import java.sql.Time;
// import java.sql.Timestamp;
import java.math.BigDecimal;

//https://learn.microsoft.com/en-us/sql/connect/jdbc/using-basic-data-types?view=sql-server-ver16
public class AllDataTypes {
    private long TBigint;
    private boolean TBit;
    private BigDecimal TDecimal;
    private int TInt;
    private BigDecimal TMoney;
    private BigDecimal TNumeric;
    private short TSmallint;
    private BigDecimal TSmallMoney;
    private short TTinyint;
    private double TFloat;
    private float TReal;
    // private Date TDate;
    // private Timestamp TDatetime;
    // private Timestamp TDatetime2;
    // private Date TDatetimeOffset;
    // private Timestamp TSmallDatetime;
    // private Time TTime;
    private String TChar;
    private String TText;
    private String TVarchar;
    private String TNchar;
    private String TNtext;
    private String TNvarchar;
    private Byte[] TBinary;
    private Byte[] TImage;
    private Byte[] TVarbinary;

    // public AllDataTypes(long tBigint, boolean tBit, BigDecimal tDecimal, int tInt, BigDecimal tMoney, BigDecimal tNumeric,
    //     short tSmallint, BigDecimal tSmallMoney, short tTinyint, double tFloat, float tReal, Date tDate, Timestamp tDatetime,
    //     Timestamp tDatetime2, Date tDatetimeOffset, Timestamp tSmallDatetime, Time tTime, char tChar, String tText,
    //     String tVarchar, String tNchar, String tNtext, String tNvarchar, Byte[] tBinary, Byte[] tImage, Byte[] tVarbinary) {
    public AllDataTypes(long tBigint, boolean tBit, BigDecimal tDecimal, int tInt, BigDecimal tMoney, BigDecimal tNumeric,
        short tSmallint, BigDecimal tSmallMoney, short tTinyint, double tFloat, float tReal, String tChar, String tText,
        String tVarchar, String tNchar, String tNtext, String tNvarchar, Byte[] tBinary, Byte[] tImage, Byte[] tVarbinary) {
        TBigint = tBigint;
        TBit = tBit;
        TDecimal = tDecimal;
        TInt = tInt;
        TMoney = tMoney;
        TNumeric = tNumeric;
        TSmallint = tSmallint;
        TSmallMoney = tSmallMoney;
        TTinyint = tTinyint;
        TFloat = tFloat;
        TReal = tReal;
        // TDate = tDate;
        // TDatetime = tDatetime;
        // TDatetime2 = tDatetime2;
        // TDatetimeOffset = tDatetimeOffset;
        // TSmallDatetime = tSmallDatetime;
        // TTime = tTime;
        TChar = tChar;
        TText = tText;
        TVarchar = tVarchar;
        TNchar = tNchar;
        TNtext = tNtext;
        TNvarchar = tNvarchar;
        TBinary = tBinary;
        TImage = tImage;
        TVarbinary = tVarbinary;
    }

    public long getTBigint() {
        return TBigint;
    }

    public void setTBigint(long tBigint) {
        TBigint = tBigint;
    }

    public boolean setTBit() {
        return TBit;
    }

    public void setTBit(boolean tBit) {
        TBit = tBit;
    }

    public BigDecimal getTDecimal() {
        return TDecimal;
    }

    public void setTDecimal(BigDecimal tDecimal) {
        TDecimal = tDecimal;
    }

    public int getTInt() {
        return TInt;
    }

    public void setTInt(int tInt) {
        TInt = tInt;
    }

    public BigDecimal getTMoney() {
        return TMoney;
    }

    public void setTMoney(BigDecimal tMoney) {
        TMoney = tMoney;
    }

    public BigDecimal getTNumeric() {
        return TNumeric;
    }

    public void setTNumeric(BigDecimal tNumeric) {
        TNumeric = tNumeric;
    }

    public short getTSmallint() {
        return TSmallint;
    }

    public void setTSmallint(short tSmallint) {
        TSmallint = tSmallint;
    }

    public BigDecimal getTSmallMoney() {
        return TSmallMoney;
    }

    public void setTSmallMoney(BigDecimal tSmallMoney) {
        TSmallMoney = tSmallMoney;
    }

    public short getTTinyint() {
        return TTinyint;
    }

    public void setTTinyint(short tTinyint) {
        TTinyint = tTinyint;
    }

    public double getTFloat() {
        return TFloat;
    }

    public void setTFloat(double tFloat) {
        TFloat = tFloat;
    }

    public float getTReal() {
        return TReal;
    }

    public void setTReal(float tReal) {
        TReal = tReal;
    }

    // public Date getTDate() {
    //     return TDate;
    // }

    // public void setTDate(Date tDate) {
    //     TDate = tDate;
    // }

    // public Timestamp getTDatetime() {
    //     return TDatetime;
    // }

    // public void setTDatetime(Timestamp tDatetime) {
    //     TDatetime = tDatetime;
    // }

    // public Timestamp getTDatetime2() {
    //     return TDatetime2;
    // }

    // public void setTDatetime2(Timestamp tDatetime2) {
    //     TDatetime2 = tDatetime2;
    // }

    // public Date getTDatetimeOffset() {
    //     return TDatetimeOffset;
    // }

    // public void setTDatetimeOffset(Date tDatetimeOffset) {
    //     TDatetimeOffset = tDatetimeOffset;
    // }

    // public Timestamp getTSmallDatetime() {
    //     return TSmallDatetime;
    // }

    // public void setTSmallDatetime(Timestamp tSmallDatetime) {
    //     TSmallDatetime = tSmallDatetime;
    // }

    // public Time getTTime() {
    //     return TTime;
    // }

    // public void setTTime(Time tTime) {
    //     TTime = tTime;
    // }

    public String getTChar() {
        return TChar;
    }

    public void setTChar(String tChar) {
        TChar = tChar;
    }

    public String getTText() {
        return TText;
    }

    public void setTText(String tText) {
        TText = tText;
    }

    public String getTVarchar() {
        return TVarchar;
    }

    public void setTVarchar(String tVarchar) {
        TVarchar = tVarchar;
    }

    public String getTNchar() {
        return TNchar;
    }

    public void setTNchar(String tNchar) {
        TNchar = tNchar;
    }

    public String getTNtext() {
        return TNtext;
    }

    public void setTNtext(String tNtext) {
        TNtext = tNtext;
    }

    public String getTNvarchar() {
        return TNvarchar;
    }

    public void setTNvarchar(String tNvarchar) {
        TNvarchar = tNvarchar;
    }

    public Byte[] getTBinary() {
        return TBinary;
    }

    public void setTBinary(Byte[] tBinary) {
        TBinary = tBinary;
    }

    public Byte[] getTImage() {
        return TImage;
    }

    public void setTImage(Byte[] tImage) {
        TImage = tImage;
    }

    public Byte[] getTVarbinary() {
        return TVarbinary;
    }

    public void setTVarbinary(Byte[] tVarbinary) {
        TVarbinary = tVarbinary;
    }
}
