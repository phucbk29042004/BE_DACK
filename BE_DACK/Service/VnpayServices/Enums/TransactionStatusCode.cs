namespace QuanLyDatVeMayBay.Services.VnpayServices.Enums
{
    using System.ComponentModel;

    public enum TransactionStatusCode : sbyte
    {

        [Description("Giao dịch thành công")]
        Code_00 = 0,

        [Description("Giao dịch chưa hoàn tất")]
        Code_01 = 1,

        [Description("Giao dịch bị lỗi")]
        Code_02 = 2,

        [Description("Khách hàng đã bị trừ tiền tại ngân hàng nhưng giao dịch chưa thành công ở VNPAY")]
        Code_04 = 4,

        [Description("VNPAY đang xử lý giao dịch này (hoàn tiền)")]
        Code_05 = 5,

        [Description("VNPAY đã gửi yêu cầu hoàn tiền đến ngân hàng (hoàn tiền)")]
        Code_06 = 6,

        [Description("Giao dịch bị nghi ngờ gian lận")]
        Code_07 = 7,

        [Description("Giao dịch hoàn trả bị từ chối")]
        Code_09 = 9
    }
}
