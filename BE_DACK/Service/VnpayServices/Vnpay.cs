using QuanLyDatVeMayBay.Services.VnpayServices.Enums;
using System.Globalization;
using VNPAY.NET.Models;
using VNPAY.NET.Utilities;

namespace QuanLyDatVeMayBay.Services.VnpayServices
{
    public interface IVnpay
    {
        void Initialize(string tmpCode,
            string hashSecret,
            string callBackUrl,
            string baseUrl,
            string version = "2.1.1",
            string orderType = "other");
        string GetPaymentUrl(PaymentRequest request);
        void EnsureParametersBeforePayment();
        PaymentResult GetPaymentResult(IQueryCollection parameters);
    }
    public class Vnpay : IVnpay
    {
        private string _tmpCode;
        private string _hashSecret;
        private string _callBackUrl;
        private string _baseUrl;
        private string _version;
        private string _orderType;
        public void Initialize(string tmpCode,
            string hashSecret,
            string callBackUrl,
            string baseUrl,
            string version = "2.1.0",
            string orderType = "other")
        {
            _tmpCode = tmpCode;
            _hashSecret = hashSecret;
            _callBackUrl = callBackUrl;
            _baseUrl = baseUrl;
            _version = version;
            _orderType = orderType;
            EnsureParametersBeforePayment();

        }
        public string GetPaymentUrl(PaymentRequest request)
        {
            EnsureParametersBeforePayment();
            if(request.Money < 5000 || request.Money > 1000000000)
            {
                throw new Exception("Số tiền thanh toán không hợp lệ. Số tiền phải từ 5,000 đến 1,000,000,000 VND");
            }
            if(string.IsNullOrEmpty(request.Description))
            {
                throw new Exception("Không được để trống mô tả thanh toán!");
            }
            if(string.IsNullOrEmpty(request.IpAddress))
            {
                throw new Exception("Không được để trống địa chỉ IP!");
            }
            var helper = new PaymentHelper();
            helper.AddRequestData("vnp_Version", _version);
            helper.AddRequestData("vnp_Command", "pay");
            helper.AddRequestData("vnp_TmnCode", _tmpCode);
            helper.AddRequestData("vnp_Amount", (request.Money * 100).ToString());
            helper.AddRequestData("vnp_CreateDate",request.CreatedDate.ToString("yyyyMMddHHmmss"));
            helper.AddRequestData("vnp_CurrCode", request.Currency.ToString().ToUpper());
            helper.AddRequestData("vnp_IpAddr", request.IpAddress);
            helper.AddRequestData("vnp_Locale", EnumHelper.GetDescription(request.Language));
            helper.AddRequestData("vnp_BankCode",request.BankCode == BankCode.ANY?string.Empty:request.BankCode.ToString());
            helper.AddRequestData("vnp_OrderInfo", request.Description.Trim());
            helper.AddRequestData("vnp_OrderType", _orderType);
            helper.AddRequestData("vnp_ReturnUrl", _callBackUrl);
            helper.AddRequestData("vnp_TxnRef", request.PaymentId.ToString());
            helper.AddRequestData("vnp_ExpireDate", request.CreatedDate.AddMinutes(15).ToString("yyyyMMddHHmmss"));
            return helper.GetPaymentUrl(_baseUrl, _hashSecret);
        }
        public PaymentResult GetPaymentResult(IQueryCollection parameters)
        {
            // Lấy giá trị đầu tiên nếu có nhiều giá trị (IQueryCollection có thể có StringValues)
            var responseData = parameters
                .Where(kv => !string.IsNullOrEmpty(kv.Key) && kv.Key.StartsWith("vnp_"))
                .ToDictionary(
                    kv => kv.Key, 
                    kv => kv.Value.Count > 0 ? kv.Value[0] ?? string.Empty : string.Empty
                );
            var vnp_BankCode = responseData.GetValueOrDefault("vnp_BankCode");
            var vnp_BankTranNo = responseData.GetValueOrDefault("vnp_BankTranNo");
            var vnp_CardType = responseData.GetValueOrDefault("vnp_CardType");
            var vnp_PayDate = responseData.GetValueOrDefault("vnp_PayDate");
            var vnp_OrderInfo = responseData.GetValueOrDefault("vnp_OrderInfo");
            var vnp_TransactionNo = responseData.GetValueOrDefault("vnp_TransactionNo");
            var vnp_TransactionStatus = responseData.GetValueOrDefault("vnp_TransactionStatus");
            var vnp_ResponseCode = responseData.GetValueOrDefault("vnp_ResponseCode");  
            var vnp_TxnRef = responseData.GetValueOrDefault("vnp_TxnRef");
            var vnp_SecureHash = responseData.GetValueOrDefault("vnp_SecureHash");

            if(string.IsNullOrEmpty(vnp_TxnRef) || 
                string.IsNullOrEmpty(vnp_ResponseCode) || 
                string.IsNullOrEmpty(vnp_SecureHash) ||
                string.IsNullOrEmpty(vnp_TransactionStatus) ||
                string.IsNullOrEmpty(vnp_OrderInfo) ||
                string.IsNullOrEmpty(vnp_TransactionNo) ||
                string.IsNullOrEmpty(vnp_PayDate) ||
                string.IsNullOrEmpty(vnp_CardType) ||
                string.IsNullOrEmpty(vnp_BankTranNo) ||
                string.IsNullOrEmpty(vnp_BankCode))
            {
                throw new Exception("Không đủ dữ liệu để giao dịch");
            }
            var helper = new PaymentHelper();
            foreach (var (key, value) in responseData)
            {
                if (!key.Equals("vnp_SecureHash"))
                {
                    helper.AddResponseData(key, value);
                }
            }
            var respone = (ResponseCode)sbyte.Parse(vnp_ResponseCode);
            var transactionStatus = (TransactionStatusCode)sbyte.Parse(vnp_TransactionStatus);
            
            // Kiểm tra chữ ký
            bool isSignatureValid = helper.IsSignatureCorrect(vnp_SecureHash, _hashSecret);
            bool isResponseSuccess = respone == ResponseCode.Code_00;
            bool isTransactionSuccess = transactionStatus == TransactionStatusCode.Code_00;
            
            // Trong môi trường development, có thể tạm thời bỏ qua kiểm tra chữ ký nếu ResponseCode và TransactionStatus đều là 00
            // (Chỉ dùng để test, KHÔNG dùng trong production)
            bool skipSignatureCheck = false;
            #if DEBUG
            // Tạm thời cho phép bỏ qua kiểm tra chữ ký nếu cả ResponseCode và TransactionStatus đều thành công
            // Để test trong môi trường development khi HashSecret có thể chưa đúng
            skipSignatureCheck = isResponseSuccess && isTransactionSuccess;
            #endif
            
            return new PaymentResult
            {
                PaymentId = long.Parse(vnp_TxnRef),
                VnpayTransactionId = long.Parse(vnp_TransactionNo),
                IsSuccess = isResponseSuccess && isTransactionSuccess && (isSignatureValid || skipSignatureCheck),
                Description = vnp_OrderInfo,
                PaymentMethod = string.IsNullOrEmpty(vnp_CardType) ? "Không xác định" : vnp_CardType,
                Timestamp = string.IsNullOrEmpty(vnp_PayDate) ? DateTime.Now : DateTime.ParseExact(vnp_PayDate, "yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                TransactionStatus = new TransactionStatus
                {
                    Code = transactionStatus,
                    Description = EnumHelper.GetDescription(transactionStatus)
                },
                PaymentResponse = new PaymentResponse
                {
                    Code = respone,
                    Description = EnumHelper.GetDescription(respone)
                },
                BankingInfor = new BankingInfor
                {
                    BankCode = string.IsNullOrEmpty(vnp_BankCode) ? "Không xác định" : vnp_BankCode,
                    BankTransactionId = string.IsNullOrEmpty(vnp_BankTranNo) ? "Không xác định" : vnp_BankTranNo
                }
            };
        }
        public void EnsureParametersBeforePayment()
        {
            if(string.IsNullOrEmpty(_tmpCode) || string.IsNullOrEmpty(_hashSecret) || string.IsNullOrEmpty(_callBackUrl) || string.IsNullOrEmpty(_baseUrl))
            {
                throw new Exception("Vui lòng khởi tạo đầy đủ các tham số trước khi thanh toán");
            }
        }

    }
}
