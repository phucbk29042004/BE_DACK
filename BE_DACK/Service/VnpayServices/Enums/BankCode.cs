using System.ComponentModel;

namespace QuanLyDatVeMayBay.Services.VnpayServices.Enums
{
    /// <summary>
    /// Các phương thức thanh toán được VNPAY hỗ trợ.  
    /// </summary>
    public enum BankCode : sbyte
    {
        [Description("Tất cả phương thức thanh toán")]
        ANY,
        [Description("Thanh toán quét mã QR")]
        VNPAYQR,
        [Description("Thẻ ATM hoặc tài khoản ngân hàng tại Việt Nam")]
        VNBANK,
        [Description("Thẻ thanh toán quốc tế")]
        INTCARD,
        [Description("Thẻ thanh toán quốc tế")]
        NCB,
    }

}
