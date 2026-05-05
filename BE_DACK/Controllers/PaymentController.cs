using System;
using System.Collections.Generic;
using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuanLyDatVeMayBay.Services.VnpayServices;
using QuanLyDatVeMayBay.Services.VnpayServices.Enums;
using VNPAY.NET.Models;
using VNPAY.NET.Utilities;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly DACKContext _context;
        private readonly IConfiguration _configuration;
        private readonly IVnpay _vnpay;
        private readonly IOptions<VNPaySettings> _cfg;
        private readonly PayOSClient _payOS;

        private string GetFrontendUrl(string path = "")
        {
            var baseUrl = _configuration["FrontendUrl:BaseUrl"] ?? "http://127.0.0.1:5500";
            baseUrl = baseUrl.TrimEnd('/');
            
            // Đảm bảo baseUrl có protocol
            if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
            {
                baseUrl = "http://" + baseUrl;
            }
            
            if (string.IsNullOrEmpty(path))
                return baseUrl;
            
            // Đảm bảo path bắt đầu bằng / và không có khoảng trắng
            path = path.Trim();
            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }
            
            var fullUrl = $"{baseUrl}{path}";
            return fullUrl;
        }

        private static readonly HashSet<string> SuccessfulPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Thành công",
            "Thanh toán COD",
            "COD",
            "Thanh toán thành công"
        };

        private static bool IsSuccessfulPayment(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return SuccessfulPaymentStatuses.Contains(status.Trim());
        }

        public PaymentController(DACKContext context, IConfiguration configuration, IVnpay vnpay, IOptions<VNPaySettings> cfg, PayOSClient payOS)
        {
            _context = context;
            _configuration = configuration;
            _vnpay = vnpay;
            _cfg = cfg;
            _payOS = payOS;
        }

        [Authorize]
        [HttpPost("ThanhToan")]
        public async Task<IActionResult> ThanhToan([FromBody] ThanhToanDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });

            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (donHang.TrangThai == "Đã hủy")
                {
                    return BadRequest(new { success = false, message = "Đơn hàng đã bị hủy, không thể thanh toán." });
                }

                if (donHang.TrangThai == "Đã thanh toán" || donHang.TrangThai == "Hoàn thành")
                {
                    return BadRequest(new { success = false, message = "Đơn hàng đã được thanh toán." });
                }

                decimal tongDaThanhToan = donHang.Payments
                    .Where(p => IsSuccessfulPayment(p.TrangThai))
                    .Sum(p => p.SoTienThanhToan);

                decimal conLai = donHang.TongGiaTriDonHang - tongDaThanhToan;

                if (dto.SoTien <= 0)
                {
                    return BadRequest(new { success = false, message = "Số tiền thanh toán phải lớn hơn 0." });
                }

                if (dto.SoTien > conLai)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Số tiền thanh toán vượt quá số tiền còn lại. Còn lại: {conLai:N0}đ"
                    });
                }

                var phuongThucHopLe = new[] { "VNPAY", "COD", "PAYOS" };
                if (!phuongThucHopLe.Contains(dto.PhuongThucThanhToan.ToUpper()))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Phương thức thanh toán không hợp lệ. Chọn: {string.Join(", ", phuongThucHopLe)}"
                    });
                }
                var phuongThuc = dto.PhuongThucThanhToan.ToUpper();
                bool isVnpay = phuongThuc == "VNPAY";
                bool isPayOS = phuongThuc == "PAYOS";

                var thanhToan = new Payment
                {
                    OrderId = dto.OrderId,
                    NgayThanhToan = DateTime.UtcNow,
                    SoTienThanhToan = dto.SoTien,
                    PhuongThucThanhToan = phuongThuc,
                    TrangThai = (isVnpay || isPayOS) ? "Chờ thanh toán" : "Thành công"
                };

                _context.Payments.Add(thanhToan);
                await _context.SaveChangesAsync();

                if (isVnpay)
                {
                    var cfg = _cfg.Value;

                    _vnpay.Initialize(
                      cfg.vnp_TmnCode,
                      cfg.vnp_HashSecret,
                      cfg.vnp_ReturnUrl,
                      cfg.vnp_Url
                     );

                    var ipAddress = NetworkHelper.GetIpAddress(HttpContext);
                    var request = new PaymentRequest
                    {
                        PaymentId = thanhToan.Id,
                        Money = (double)dto.SoTien,
                        Description = "Thanh toán đồ nội thất!",
                        IpAddress = ipAddress,
                        CreatedDate = DateTime.Now,
                        Currency = Currency.VND,
                        Language = DisplayLanguage.Vietnamese
                    };
                    var url = _vnpay.GetPaymentUrl(request);
                    return Ok(new
                    {
                        success = true,
                        code = 202,
                        url,
                        message = "Đang chuyển sang cổng thanh toán VNPAY."
                    });
                }

                if (isPayOS)
                {
                    try
                    {
                        var returnUrl = _configuration["PayOS:ReturnUrl"] ?? GetFrontendUrl("/api/Payment/ReturnPayOS");
                        var cancelUrl = _configuration["PayOS:CancelUrl"] ?? GetFrontendUrl("/src/payment.html?orderId=" + dto.OrderId);

                        // Lấy thông tin đơn hàng để đưa vào PayOS
                        var items = donHang.OrderDetails.Select(d => new PaymentLinkItem {
                            Name = d.Product?.TenSp ?? "Sản phẩm",
                            Quantity = 1,
                            Price = (long)d.Gia
                        }).ToList();
                        
                        // PayOS yêu cầu orderCode là số long và duy nhất
                        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                        long orderCode = long.Parse(timestamp.Substring(timestamp.Length - 10) + thanhToan.Id.ToString().PadLeft(3, '0'));
                        
                        var paymentRequest = new CreatePaymentLinkRequest 
                        {
                            OrderCode = orderCode,
                            Amount = (long)dto.SoTien,
                            Description = $"Thanh toan don hang {donHang.Id}",
                            Items = items,
                            ReturnUrl = returnUrl,
                            CancelUrl = cancelUrl
                        };

                        var createPaymentResult = await _payOS.PaymentRequests.CreateAsync(paymentRequest);
                        
                        return Ok(new
                        {
                            success = true,
                            code = 202,
                            url = createPaymentResult.CheckoutUrl,
                            message = "Đang chuyển sang cổng thanh toán PayOS."
                        });
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(new { success = false, message = "Lỗi khi tạo link thanh toán PayOS: " + ex.Message });
                    }
                }

                tongDaThanhToan += dto.SoTien;

                if (tongDaThanhToan >= donHang.TongGiaTriDonHang)
                {
                    donHang.TrangThai = "Đã thanh toán";

                    foreach (var detail in donHang.OrderDetails)
                    {
                        detail.TrangThai = "Đã thanh toán";
                    }
                }
                else
                {
                    donHang.TrangThai = "Thanh toán một phần";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Đã ghi nhận thanh toán COD thành công."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi thanh toán.",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpGet("LichSuThanhToan/{orderId}")]
        public async Task<IActionResult> LichSuThanhToan(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                var lichSu = donHang.Payments
                    .OrderByDescending(p => p.NgayThanhToan)
                    .Select(p => new
                    {
                        paymentId = p.Id,
                        ngayThanhToan = p.NgayThanhToan,
                        soTien = p.SoTienThanhToan,
                        phuongThuc = p.PhuongThucThanhToan,
                        trangThai = p.TrangThai
                    })
                    .ToList();

                decimal tongDaThanhToan = donHang.Payments
                    .Where(p => IsSuccessfulPayment(p.TrangThai))
                    .Sum(p => p.SoTienThanhToan);

                return Ok(new
                {
                    success = true,
                    message = "Lấy lịch sử thanh toán thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        tongGiaTriDonHang = donHang.TongGiaTriDonHang,
                        tongDaThanhToan = tongDaThanhToan,
                        conLai = donHang.TongGiaTriDonHang - tongDaThanhToan,
                        trangThaiDonHang = donHang.TrangThai,
                        lichSuThanhToan = lichSu
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy lịch sử thanh toán.",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpGet("TatCaThanhToan")]
        public async Task<IActionResult> TatCaThanhToan()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var danhSach = await _context.Payments
                    .Include(p => p.Order)
                    .Where(p => p.Order.CustomerId == userId)
                    .OrderByDescending(p => p.NgayThanhToan)
                    .Select(p => new
                    {
                        paymentId = p.Id,
                        orderId = p.OrderId,
                        ngayThanhToan = p.NgayThanhToan,
                        soTien = p.SoTienThanhToan,
                        phuongThuc = p.PhuongThucThanhToan,
                        trangThai = p.TrangThai,
                        trangThaiDonHang = p.Order.TrangThai
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách thanh toán thành công.",
                    tongSoGiaoDich = danhSach.Count,
                    tongSoTien = danhSach.Where(p => IsSuccessfulPayment(p.trangThai)).Sum(p => p.soTien),
                    data = danhSach
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách thanh toán.",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpGet("KiemTraThanhToan/{orderId}")]
        public async Task<IActionResult> KiemTraThanhToan(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                decimal tongDaThanhToan = donHang.Payments
                    .Where(p => IsSuccessfulPayment(p.TrangThai))
                    .Sum(p => p.SoTienThanhToan);

                decimal conLai = donHang.TongGiaTriDonHang - tongDaThanhToan;
                bool daThanhToanDu = conLai <= 0;

                return Ok(new
                {
                    success = true,
                    message = "Kiểm tra trạng thái thanh toán thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        tongGiaTri = donHang.TongGiaTriDonHang,
                        daThanhToan = tongDaThanhToan,
                        conLai = conLai,
                        daThanhToanDu = daThanhToanDu,
                        trangThai = donHang.TrangThai,
                        soLanThanhToan = donHang.Payments.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi kiểm tra thanh toán.",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPut("HuyThanhToan/{paymentId}")]
        public async Task<IActionResult> HuyThanhToan(int paymentId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var thanhToan = await _context.Payments
                    .Include(p => p.Order)
                        .ThenInclude(o => o.OrderDetails)
                    .FirstOrDefaultAsync(p => p.Id == paymentId && p.Order.CustomerId == userId);

                if (thanhToan == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy giao dịch thanh toán." });
                }

                if (thanhToan.TrangThai == "Đã hủy")
                {
                    return BadRequest(new { success = false, message = "Giao dịch này đã bị hủy trước đó." });
                }

                var khoangThoiGian = DateTime.UtcNow - thanhToan.NgayThanhToan;
                if (khoangThoiGian.TotalHours > 24)
                {
                    return BadRequest(new { success = false, message = "Chỉ có thể hủy thanh toán trong vòng 24 giờ." });
                }

                if (thanhToan.Order.TrangThai == "Đang giao hàng" || thanhToan.Order.TrangThai == "Hoàn thành")
                {
                    return BadRequest(new { success = false, message = "Không thể hủy thanh toán khi đơn hàng đang/đã giao." });
                }

                thanhToan.TrangThai = "Đã hủy";

                var tongConLai = thanhToan.Order.Payments
                    .Where(p => IsSuccessfulPayment(p.TrangThai) && p.Id != paymentId)
                    .Sum(p => p.SoTienThanhToan);

                if (tongConLai <= 0)
                {
                    thanhToan.Order.TrangThai = "Chờ xác nhận";
                    foreach (var detail in thanhToan.Order.OrderDetails)
                    {
                        detail.TrangThai = "Chờ xác nhận";
                    }
                }
                else if (tongConLai < thanhToan.Order.TongGiaTriDonHang)
                {
                    thanhToan.Order.TrangThai = "Thanh toán một phần";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Hủy thanh toán thành công. Số tiền sẽ được hoàn lại trong 3-5 ngày.",
                    data = new
                    {
                        paymentId = thanhToan.Id,
                        soTienHoan = thanhToan.SoTienThanhToan,
                        trangThaiDonHang = thanhToan.Order.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi hủy thanh toán.",
                    error = ex.Message
                });
            }
        }

        [AllowAnonymous]
        [AcceptVerbs("GET", "POST")] // VNPAY có thể gọi bằng GET hoặc POST
        [Route("ReturnVnPay")]
        public async Task<IActionResult> ReturnVnPay()
        {
            // VNPAY thường gửi dữ liệu qua query string (GET) hoặc form data (POST)
            if (!Request.QueryString.HasValue && (Request.ContentLength == null || Request.ContentLength == 0))
            {
                return BadRequest(new { message = "Thiếu thông tin thanh toán từ VNPAY" });
            }

            try
            {
                var paymentPageUrl = GetFrontendUrl(_configuration["FrontendUrl:PaymentPage"] ?? "/src/payment.html");
                var ordersPageUrl = GetFrontendUrl(_configuration["FrontendUrl:OrdersPage"] ?? "/FE_DACK/src/orders.html");
                var homePageUrl = GetFrontendUrl(_configuration["FrontendUrl:HomePage"] ?? "/FE_DACK/src/index.html");
                
                Console.WriteLine($"[Payment Return] HomePageUrl: {homePageUrl}");
                Console.WriteLine($"[Payment Return] OrdersPageUrl: {ordersPageUrl}");
                Console.WriteLine($"[Payment Return] PaymentPageUrl: {paymentPageUrl}");

                PaymentResult paymentResult;
                string errorMessage = "";

                try
                {
                    IQueryCollection queryParams = Request.Query;
                    
                    if (Request.HasFormContentType && Request.Form != null && Request.Form.Count > 0)
                    {
                        // Nếu có form data, tạo một QueryCollection mới kết hợp cả query và form
                        var combinedParams = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
                        
                        // Thêm từ query string
                        foreach (var param in Request.Query)
                        {
                            combinedParams[param.Key] = param.Value;
                        }
                        
                        // Thêm từ form data (ưu tiên form data nếu trùng)
                        foreach (var param in Request.Form)
                        {
                            combinedParams[param.Key] = param.Value;
                        }
                        
                        // Tạo QueryCollection từ dictionary
                        queryParams = new Microsoft.AspNetCore.Http.QueryCollection(combinedParams);
                    }
                    
                    paymentResult = _vnpay.GetPaymentResult(queryParams);
                }
                catch (Exception ex)
                {
                    errorMessage = $"Lỗi khi xử lý kết quả thanh toán: {ex.Message}";

                    string errorHtml = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Lỗi thanh toán - Decora</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #eff2f1; min-height: 100vh; display:flex; align-items:center; justify-content:center; padding:20px; }}
        .result-container {{ background-color: #ffffff; padding: 50px 40px; border-radius: 16px; text-align:center; box-shadow: 0 6px 25px rgba(0,0,0,0.1); max-width: 520px; width: 100%; }}
        .icon {{ width: 80px; height: 80px; border-radius: 50%; display:flex; align-items:center; justify-content:center; margin: 0 auto 30px; font-size:48px; font-weight:700; }}
        .icon.error {{ background-color: #fce4e4; color: #dc2626; }}
        h1 {{ font-weight:700; color:#2f2f2f; margin-bottom:15px; font-size:28px; }}
        p {{ color:#6a6a6a; margin-bottom:25px; }}
        .error-detail {{ color:#dc2626; font-size:12px; margin-top:10px; }}
        .btn {{ padding:12px 30px; border-radius:8px; font-weight:600; text-decoration:none; display:inline-block; }}
        .btn-primary {{ background:#2f2f2f; color:#ffffff; margin-right:10px; }}
        .btn-primary:hover {{ background:#3b5d50; color:#ffffff; }}
        .btn-outline {{ border:1px solid #d1d5db; color:#374151; }}
    </style>
</head>
<body>
    <div class='result-container'>
        <div class='icon error'>!</div>
        <h1>Lỗi xử lý thanh toán</h1>
        <p>Không thể xử lý kết quả thanh toán từ VNPAY.</p>
        <p class='error-detail'>{errorMessage}</p>
        <div>
            <a href='{paymentPageUrl}' class='btn btn-primary'>Thử lại</a>
            <a href='{ordersPageUrl}' class='btn btn-outline'>Xem đơn hàng</a>
        </div>
    </div>
</body>
</html>";
                    return Content(errorHtml, "text/html");
                }

                var thanhToan = await _context.Payments.FirstOrDefaultAsync(p => p.Id == (int)paymentResult.PaymentId);
                if (thanhToan == null)
                {
                    return BadRequest(new
                    {
                        message = "Không tìm thấy thông tin thanh toán"
                    });
                }

                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == thanhToan.OrderId);

                paymentPageUrl = paymentPageUrl + $"?orderId={thanhToan.OrderId}";

                if (!paymentResult.IsSuccess)
                {
                    var reasons = new List<string>();

                    if (paymentResult.PaymentResponse != null)
                    {
                        if (paymentResult.PaymentResponse.Code != ResponseCode.Code_00)
                        {
                            reasons.Add($"Mã phản hồi: {paymentResult.PaymentResponse.Description ?? paymentResult.PaymentResponse.Code.ToString()}");
                        }
                    }

                    if (paymentResult.TransactionStatus != null)
                    {
                        if (paymentResult.TransactionStatus.Code != TransactionStatusCode.Code_00)
                        {
                            reasons.Add($"Trạng thái giao dịch: {paymentResult.TransactionStatus.Description ?? paymentResult.TransactionStatus.Code.ToString()}");
                        }
                    }

                    bool responseOk = paymentResult.PaymentResponse?.Code == ResponseCode.Code_00;
                    bool transactionOk = paymentResult.TransactionStatus?.Code == TransactionStatusCode.Code_00;

                    if (responseOk && transactionOk && reasons.Count == 0)
                    {
                        reasons.Add("Chữ ký xác thực không hợp lệ. Có thể do cấu hình không đúng hoặc dữ liệu bị thay đổi.");
                    }

                    string failureReason = reasons.Count > 0
                        ? string.Join(" | ", reasons)
                        : "Thanh toán thất bại. Vui lòng thử lại hoặc liên hệ hỗ trợ.";

                    thanhToan.TrangThai = "Thất bại";
                    await _context.SaveChangesAsync();

                    string failHtml = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thanh toán thất bại - Decora</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #eff2f1; min-height: 100vh; display:flex; align-items:center; justify-content:center; padding:20px; }}
        .result-container {{ background-color: #ffffff; padding: 50px 40px; border-radius: 16px; text-align:center; box-shadow: 0 6px 25px rgba(0,0,0,0.1); max-width: 520px; width: 100%; }}
        .icon {{ width: 80px; height: 80px; border-radius: 50%; display:flex; align-items:center; justify-content:center; margin: 0 auto 30px; font-size:48px; font-weight:700; }}
        .icon.error {{ background-color: #fce4e4; color: #dc2626; }}
        h1 {{ font-weight:700; color:#2f2f2f; margin-bottom:15px; font-size:28px; }}
        p {{ color:#6a6a6a; margin-bottom:25px; }}
        .btn {{ padding:12px 30px; border-radius:8px; font-weight:600; text-decoration:none; display:inline-block; }}
        .btn-primary {{ background:#2f2f2f; color:#ffffff; margin-right:10px; }}
        .btn-primary:hover {{ background:#3b5d50; color:#ffffff; }}
        .btn-outline {{ border:1px solid #d1d5db; color:#374151; }}
    </style>
</head>
<body>
    <div class='result-container'>
        <div class='icon error'>!</div>
        <h1>Thanh toán không thành công</h1>
        <p>Giao dịch của bạn chưa được hoàn tất. Vui lòng thử lại hoặc chọn phương thức thanh toán khác.</p>
        {(string.IsNullOrEmpty(failureReason) ? "" : $"<p class='error-detail' style='color:#dc2626; font-size:12px; margin-top:10px;'>{failureReason}</p>")}
        <div>
            <a href='{paymentPageUrl}' class='btn btn-primary'>Thử lại</a>
            <a href='{ordersPageUrl}' class='btn btn-outline'>Xem đơn hàng</a>
        </div>
    </div>
</body>
</html>";

                    return Content(failHtml, "text/html");
                }

                thanhToan.TrangThai = "Thành công";
                _context.Payments.Update(thanhToan);

                if (donHang != null)
                {
                    var tongDaThanhToan = donHang.Payments
                        .Where(p => IsSuccessfulPayment(p.TrangThai) || p.Id == thanhToan.Id)
                        .Sum(p => p.SoTienThanhToan);

                    if (tongDaThanhToan >= donHang.TongGiaTriDonHang)
                    {
                        donHang.TrangThai = "Đã thanh toán";
                        foreach (var detail in donHang.OrderDetails)
                        {
                            detail.TrangThai = "Đã thanh toán";
                        }
                    }
                    else
                    {
                        donHang.TrangThai = "Thanh toán một phần";
                    }

                    _context.Orders.Update(donHang);
                }

                await _context.SaveChangesAsync();

                string html = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thanh toán thành công - Decora</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            font-weight: 400;
            line-height: 28px;
            color: #6a6a6a;
            font-size: 14px;
            background-color: #eff2f1;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            padding: 20px;
        }}
        .success-container {{
            background-color: #ffffff;
            text-align: center;
            padding: 60px 40px;
            border-radius: 16px;
            box-shadow: 0 6px 25px rgba(0, 0, 0, 0.1);
            max-width: 500px;
            width: 100%;
        }}
        .checkmark-icon {{
            width: 80px;
            height: 80px;
            background-color: #3b5d50;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 30px;
            color: #ffffff;
            font-size: 48px;
            font-weight: 700;
        }}
        h1 {{
            font-weight: 700;
            color: #2f2f2f;
            margin-bottom: 15px;
            font-size: 32px;
        }}
        .success-message {{
            color: #6a6a6a;
            margin-bottom: 30px;
            font-size: 16px;
            line-height: 1.6;
        }}
        .countdown {{
            color: #3b5d50;
            font-weight: 600;
            margin-bottom: 30px;
            font-size: 14px;
        }}
        .btn {{
            font-weight: 600;
            padding: 12px 30px;
            border-radius: 8px;
            color: #ffffff;
            font-size: 0.9rem;
            background: #2f2f2f;
            border-color: #2f2f2f;
            text-decoration: none;
            display: inline-block;
            transition: all 0.2s ease;
            border: none;
            cursor: pointer;
        }}
        .btn:hover {{
            background: #3b5d50;
            border-color: #3b5d50;
            color: #ffffff;
            text-decoration: none;
        }}
        .btn-secondary {{
            background: #f9bf29;
            border-color: #f9bf29;
            color: #2f2f2f;
            margin-left: 15px;
        }}
        .btn-secondary:hover {{
            background: #e6a91f;
            border-color: #e6a91f;
            color: #2f2f2f;
        }}
    </style>
    <script>
        let countdown = 5;
        const countdownElement = document.getElementById('countdown');
        const homePageUrl = '{homePageUrl}';
        const ordersPageUrl = '{ordersPageUrl}';
        
        function updateCountdown() {{
            if (countdownElement) {{
                countdownElement.textContent = 'Tự động chuyển về trang chủ sau ' + countdown + ' giây...';
            }}
            countdown--;
            if (countdown < 0) {{
                goToHome();
            }}
        }}
        
        function goToHome() {{
            try {{
                console.log('Redirecting to home:', homePageUrl);
                if (homePageUrl && homePageUrl.trim() !== '') {{
                    window.location.replace(homePageUrl);
                }} else {{
                    console.error('HomePageUrl is empty');
                    window.location.replace('http://127.0.0.1:5500/FE_DACK/src/orders.html');
                }}
            }} catch (error) {{
                console.error('Error redirecting to home:', error);
                // Fallback: thử dùng absolute URL
                window.location.replace('http://127.0.0.1:5500/FE_DACK/src/index.html');
            }}
        }}
        
        function goToOrders() {{
            try {{
                console.log('Redirecting to orders:', ordersPageUrl);
                if (ordersPageUrl && ordersPageUrl.trim() !== '') {{
                    window.location.replace(ordersPageUrl);
                }} else {{
                    console.error('OrdersPageUrl is empty');
                    window.location.replace('http://127.0.0.1:5500/FE_DACK/src/orders.html');
                }}
            }} catch (error) {{
                console.error('Error redirecting to orders:', error);
                // Fallback: thử dùng absolute URL
                window.location.replace('http://127.0.0.1:5500/FE_DACK/src/orders.html');
            }}
        }}
        
        window.onload = function() {{
            setInterval(updateCountdown, 1000);
        }};
    </script>
</head>
<body>
    <div class='success-container'>
        <div class='checkmark-icon'>✓</div>
        <h1>Thanh toán thành công!</h1>
        <p class='success-message'>Cảm ơn bạn đã mua hàng tại Decora.<br>Đơn hàng của bạn đang được xử lý và sẽ được giao trong thời gian sớm nhất.</p>
        <p class='countdown' id='countdown'>Tự động chuyển về trang chủ sau 5 giây...</p>
        <div>
            <button onclick='goToHome()' class='btn'>Về trang chủ</button>
            <button onclick='goToOrders()' class='btn btn-secondary'>Xem đơn hàng</button>
        </div>
    </div>
</body>
</html>";

                return Content(html, "text/html");

            }
            catch (Exception)
            {
                return BadRequest(new { success = false, message = "Lỗi thanh toán." });
            }
        }

        [AllowAnonymous]
        [HttpGet("ReturnPayOS")]
        public async Task<IActionResult> ReturnPayOS([FromQuery] string status, [FromQuery] long orderCode)
        {
            try
            {
                var homePageUrl = GetFrontendUrl(_configuration["FrontendUrl:HomePage"] ?? "/src/index.html");
                var ordersPageUrl = GetFrontendUrl(_configuration["FrontendUrl:OrdersPage"] ?? "/src/orders.html");
                var paymentPageUrl = GetFrontendUrl(_configuration["FrontendUrl:PaymentPage"] ?? "/src/payment.html");

                if (status == "PAID")
                {
                    // Lấy ID thật từ orderCode (Timestamp 10 chữ số + ID)
                    string codeStr = orderCode.ToString();
                    int paymentId = int.Parse(codeStr.Substring(10));

                    var thanhToan = await _context.Payments
                        .Include(p => p.Order)
                            .ThenInclude(o => o.OrderDetails)
                        .FirstOrDefaultAsync(p => p.Id == paymentId);

                    if (thanhToan != null && thanhToan.TrangThai != "Thành công")
                    {
                        thanhToan.TrangThai = "Thành công";
                        
                        var donHang = thanhToan.Order;
                        var tongDaThanhToan = donHang.Payments
                            .Where(p => IsSuccessfulPayment(p.TrangThai) || p.Id == thanhToan.Id)
                            .Sum(p => p.SoTienThanhToan);

                        if (tongDaThanhToan >= donHang.TongGiaTriDonHang)
                        {
                            donHang.TrangThai = "Đã thanh toán";
                            foreach (var detail in donHang.OrderDetails)
                            {
                                detail.TrangThai = "Đã thanh toán";
                            }
                        }
                        else
                        {
                            donHang.TrangThai = "Thanh toán một phần";
                        }
                        
                        await _context.SaveChangesAsync();
                    }

                    string successHtml = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thanh toán thành công - Decora</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #eff2f1; min-height: 100vh; display:flex; align-items:center; justify-content:center; padding:20px; }}
        .result-container {{ background-color: #ffffff; padding: 50px 40px; border-radius: 16px; text-align:center; box-shadow: 0 6px 25px rgba(0,0,0,0.1); max-width: 500px; width: 100%; }}
        .icon {{ width: 80px; height: 80px; background-color: #3b5d50; border-radius: 50%; display:flex; align-items:center; justify-content:center; margin: 0 auto 30px; color:#ffffff; font-size:48px; font-weight:700; }}
        h1 {{ font-weight:700; color:#2f2f2f; margin-bottom:15px; font-size:32px; }}
        p {{ color:#6a6a6a; margin-bottom:30px; }}
        .btn {{ padding:12px 30px; border-radius:8px; font-weight:600; text-decoration:none; display:inline-block; }}
        .btn-primary {{ background:#2f2f2f; color:#ffffff; margin-right:10px; }}
        .btn-secondary {{ background:#f9bf29; color:#2f2f2f; }}
    </style>
</head>
<body>
    <div class='result-container'>
        <div class='icon'>✓</div>
        <h1>Thanh toán thành công!</h1>
        <p>Cảm ơn bạn đã mua hàng. Đơn hàng của bạn đã được thanh toán thành công.</p>
        <div>
            <a href='{homePageUrl}' class='btn btn-primary'>Về trang chủ</a>
            <a href='{ordersPageUrl}' class='btn btn-secondary'>Xem đơn hàng</a>
        </div>
    </div>
</body>
</html>";
                    return Content(successHtml, "text/html");
                }
                else
                {
                    string failHtml = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thanh toán thất bại - Decora</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #eff2f1; min-height: 100vh; display:flex; align-items:center; justify-content:center; padding:20px; }}
        .result-container {{ background-color: #ffffff; padding: 50px 40px; border-radius: 16px; text-align:center; box-shadow: 0 6px 25px rgba(0,0,0,0.1); max-width: 500px; width: 100%; }}
        .icon {{ width: 80px; height: 80px; background-color: #fce4e4; color: #dc2626; border-radius: 50%; display:flex; align-items:center; justify-content:center; margin: 0 auto 30px; font-size:48px; font-weight:700; }}
        h1 {{ font-weight:700; color:#2f2f2f; margin-bottom:15px; font-size:28px; }}
        p {{ color:#6a6a6a; margin-bottom:30px; }}
        .btn {{ padding:12px 30px; border-radius:8px; font-weight:600; text-decoration:none; display:inline-block; }}
        .btn-primary {{ background:#2f2f2f; color:#ffffff; }}
    </style>
</head>
<body>
    <div class='result-container'>
        <div class='icon'>!</div>
        <h1>Thanh toán không thành công</h1>
        <p>Giao dịch của bạn đã bị hủy hoặc gặp lỗi. Vui lòng thử lại.</p>
        <a href='{paymentPageUrl}' class='btn btn-primary'>Thử lại</a>
    </div>
</body>
</html>";
                    return Content(failHtml, "text/html");
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi xử lý kết quả PayOS: " + ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("PayOSWebhook")]
        public async Task<IActionResult> PayOSWebhook([FromBody] Webhook webhookData)
        {
            try
            {
                // Xác thực webhook từ PayOS
                var verifiedData = await _payOS.Webhooks.VerifyAsync(webhookData);
                
                if (verifiedData.Code == "00")
                {
                    string codeStr = verifiedData.OrderCode.ToString();
                    int paymentId = int.Parse(codeStr.Substring(10));

                    var thanhToan = await _context.Payments
                        .Include(p => p.Order)
                            .ThenInclude(o => o.OrderDetails)
                        .FirstOrDefaultAsync(p => p.Id == paymentId);

                    if (thanhToan != null && thanhToan.TrangThai != "Thành công")
                    {
                        thanhToan.TrangThai = "Thành công";
                        
                        var donHang = thanhToan.Order;
                        var tongDaThanhToan = donHang.Payments
                            .Where(p => IsSuccessfulPayment(p.TrangThai) || p.Id == thanhToan.Id)
                            .Sum(p => p.SoTienThanhToan);

                        if (tongDaThanhToan >= donHang.TongGiaTriDonHang)
                        {
                            donHang.TrangThai = "Đã thanh toán";
                            foreach (var detail in donHang.OrderDetails)
                            {
                                detail.TrangThai = "Đã thanh toán";
                            }
                        }
                        else
                        {
                            donHang.TrangThai = "Thanh toán một phần";
                        }
                        
                        await _context.SaveChangesAsync();
                    }
                }
                
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine("PayOS Webhook Error: " + ex.Message);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    // DTO
    public class ThanhToanDto
    {
        public int OrderId { get; set; }
        public decimal SoTien { get; set; }
        public string PhuongThucThanhToan { get; set; }
    }
}
