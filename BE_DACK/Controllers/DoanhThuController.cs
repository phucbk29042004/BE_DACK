using System;
using System.Collections.Generic;
using System.Globalization;
using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoanhThuController : ControllerBase
    {
        private readonly DACKContext _context;
        private static readonly string[] AcceptedDateFormats = { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
        private const string PAYMENT_SUCCESS = "Thành công";
        private static readonly HashSet<string> SuccessfulPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            PAYMENT_SUCCESS,
            "Thanh toán COD",
            "Thanh toán thành công",
            "COD"
        };

        public DoanhThuController(DACKContext context)
        {
            _context = context;
        }

        private static bool IsSuccessfulPayment(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return SuccessfulPaymentStatuses.Contains(status.Trim());
        }

        private IQueryable<Payment> BasePaymentQuery()
        {
            return _context.Payments
                .Include(p => p.Order)
                    .ThenInclude(o => o.Customer)
                .Where(p => SuccessfulPaymentStatuses.Contains(p.TrangThai.Trim()));
        }

        #region Debug Endpoints

        [HttpGet("Debug")]
        public async Task<IActionResult> DebugPayments()
        {
            var allPayments = await _context.Payments
                .Include(p => p.Order)
                .Select(p => new
                {
                    p.Id,
                    p.OrderId,
                    p.TrangThai,
                    TrangThaiLength = p.TrangThai != null ? p.TrangThai.Length : 0,
                    p.NgayThanhToan,
                    p.SoTienThanhToan,
                    p.PhuongThucThanhToan,
                    OrderTrangThai = p.Order != null ? p.Order.TrangThai : null
                })
                .ToListAsync();

            var summary = new
            {
                totalPayments = allPayments.Count,
                successfulPayments = allPayments.Count(p => IsSuccessfulPayment(p.TrangThai)),
                distinctStatuses = allPayments
                    .Where(p => p.TrangThai != null)
                    .Select(p => p.TrangThai.Trim())
                    .Distinct()
                    .ToList(),
                payments = allPayments
            };

            return Ok(summary);
        }

        [HttpGet("DebugOrders")]
        public async Task<IActionResult> DebugOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.Payments)
                .Include(o => o.Customer)
                .Select(o => new
                {
                    o.Id,
                    o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.HoTen : null,
                    o.NgayTaoDonHang,
                    o.TongGiaTriDonHang,
                    o.TrangThai,
                    PaymentCount = o.Payments.Count,
                    Payments = o.Payments.Select(p => new
                    {
                        p.Id,
                        p.TrangThai,
                        p.SoTienThanhToan,
                        p.NgayThanhToan
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new
            {
                totalOrders = orders.Count,
                ordersWithPayments = orders.Count(o => o.PaymentCount > 0),
                orders
            });
        }

        #endregion

        #region Theo Ngày / Tháng / Năm

        [HttpGet("TheoNgay")]
        public async Task<IActionResult> GetByDate([FromQuery] string ngay)
        {
            if (string.IsNullOrWhiteSpace(ngay))
            {
                return BadRequest(new { success = false, message = "Vui lòng nhập ngày theo định dạng dd-MM-yyyy." });
            }

            if (!DateTime.TryParseExact(ngay, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ngayFilter))
            {
                return BadRequest(new { success = false, message = "Định dạng ngày không hợp lệ. Ví dụ: 20-11-2025" });
            }

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Date == ngayFilter.Date)
                .ToListAsync();

            var chiTiet = payments.Select(p => new
            {
                paymentId = p.Id,
                orderId = p.OrderId,
                soTien = p.SoTienThanhToan,
                phuongThuc = p.PhuongThucThanhToan,
                ngayThanhToan = p.NgayThanhToan,
                order = p.Order != null ? new
                {
                    orderId = p.Order.Id,
                    ngayTao = p.Order.NgayTaoDonHang,
                    tongGiaTri = p.Order.TongGiaTriDonHang,
                    trangThai = p.Order.TrangThai,
                    khachHang = p.Order.Customer != null ? new
                    {
                        id = p.Order.Customer.Id,
                        hoTen = p.Order.Customer.HoTen,
                        email = p.Order.Customer.Email,
                        sdt = p.Order.Customer.Sdt
                    } : null
                } : null
            }).ToList();

            return Ok(new
            {
                success = true,
                ngay = ngayFilter.ToString("dd/MM/yyyy"),
                tongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                soDon = payments.Count,
                chiTiet
            });
        }

        [HttpGet("TheoThang")]
        public async Task<IActionResult> GetByMonth([FromQuery] int thang, [FromQuery] int nam)
        {
            if (thang < 1 || thang > 12)
                return BadRequest(new { success = false, message = "Tháng không hợp lệ (1-12)." });

            if (nam < 2000 || nam > 2100)
                return BadRequest(new { success = false, message = "Năm không hợp lệ (2000-2100)." });

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Month == thang && p.NgayThanhToan.Year == nam)
                .ToListAsync();

            var chiTietTheoNgay = payments
                .GroupBy(p => p.NgayThanhToan.Date)
                .Select(g => new
                {
                    ngay = g.Key.ToString("dd/MM/yyyy"),
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count(),
                    chiTiet = g.Select(p => new
                    {
                        paymentId = p.Id,
                        orderId = p.OrderId,
                        soTien = p.SoTienThanhToan,
                        phuongThuc = p.PhuongThucThanhToan
                    }).ToList()
                })
                .OrderBy(x => DateTime.ParseExact(x.ngay, "dd/MM/yyyy", CultureInfo.InvariantCulture))
                .ToList();

            return Ok(new
            {
                success = true,
                thang = thang.ToString("00"),
                nam,
                tongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                soDon = payments.Count,
                chiTietTheoNgay
            });
        }

        [HttpGet("TheoNam")]
        public async Task<IActionResult> GetByYear([FromQuery] int nam)
        {
            if (nam < 2000 || nam > 2100)
                return BadRequest(new { success = false, message = "Năm không hợp lệ (2000-2100)." });

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Year == nam)
                .ToListAsync();

            var chiTietTheoThang = payments
                .GroupBy(p => p.NgayThanhToan.Month)
                .Select(g => new
                {
                    thang = g.Key,
                    tenThang = $"Tháng {g.Key}",
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count()
                })
                .OrderBy(x => x.thang)
                .ToList();

            return Ok(new
            {
                success = true,
                nam,
                tongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                soDon = payments.Count,
                chiTietTheoThang
            });
        }

        #endregion

        #region Khoảng thời gian & Thống kê chung

        [HttpGet("TheoKhoangThoiGian")]
        public async Task<IActionResult> GetByRange([FromQuery] string tuNgay, [FromQuery] string denNgay)
        {
            if (!DateTime.TryParseExact(tuNgay, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tuNgayFilter) ||
                !DateTime.TryParseExact(denNgay, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var denNgayFilter))
            {
                return BadRequest(new { success = false, message = "Định dạng ngày không hợp lệ." });
            }

            if (tuNgayFilter > denNgayFilter)
                return BadRequest(new { success = false, message = "Từ ngày phải nhỏ hơn hoặc bằng đến ngày." });

            var payments = await BasePaymentQuery()
                .Where(p => p.NgayThanhToan.Date >= tuNgayFilter.Date && p.NgayThanhToan.Date <= denNgayFilter.Date)
                .ToListAsync();

            var chiTietTheoNgay = payments
                .GroupBy(p => p.NgayThanhToan.Date)
                .Select(g => new
                {
                    ngay = g.Key.ToString("dd/MM/yyyy"),
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count()
                })
                .OrderBy(x => DateTime.ParseExact(x.ngay, "dd/MM/yyyy", CultureInfo.InvariantCulture))
                .ToList();

            var soNgay = (denNgayFilter - tuNgayFilter).Days + 1;

            return Ok(new
            {
                success = true,
                tuNgay = tuNgayFilter.ToString("dd/MM/yyyy"),
                denNgay = denNgayFilter.ToString("dd/MM/yyyy"),
                soNgay,
                tongDoanhThu = payments.Sum(p => p.SoTienThanhToan),
                soDon = payments.Count,
                doanhThuTrungBinh = payments.Count > 0 ? payments.Sum(p => p.SoTienThanhToan) / soNgay : 0,
                chiTietTheoNgay
            });
        }

        [HttpGet("ThongKeChung")]
        public async Task<IActionResult> GetOverview()
        {
            var tatCaThanhToan = await BasePaymentQuery().ToListAsync();

            var homNay = DateTime.Today;
            var thangHienTai = DateTime.Now.Month;
            var namHienTai = DateTime.Now.Year;

            var doanhThuHomNay = tatCaThanhToan.Where(p => p.NgayThanhToan.Date == homNay).ToList();
            var doanhThuThangNay = tatCaThanhToan.Where(p => p.NgayThanhToan.Month == thangHienTai && p.NgayThanhToan.Year == namHienTai).ToList();
            var doanhThuNamNay = tatCaThanhToan.Where(p => p.NgayThanhToan.Year == namHienTai).ToList();

            var topKhachHang = tatCaThanhToan
                .Where(p => p.Order?.Customer != null)
                .GroupBy(p => new
                {
                    p.Order!.Customer!.Id,
                    p.Order.Customer.HoTen,
                    p.Order.Customer.Email,
                    p.Order.Customer.Sdt
                })
                .Select(g => new
                {
                    khachHangId = g.Key.Id,
                    hoTen = g.Key.HoTen,
                    email = g.Key.Email,
                    sdt = g.Key.Sdt,
                    tongChi = g.Sum(x => x.SoTienThanhToan),
                    soDon = g.Count()
                })
                .OrderByDescending(x => x.tongChi)
                .Take(5)
                .ToList();

            var thongKePhuongThuc = tatCaThanhToan
                .GroupBy(p => p.PhuongThucThanhToan ?? "Không xác định")
                .Select(g => new
                {
                    phuongThuc = g.Key,
                    soLuong = g.Count(),
                    tongTien = g.Sum(x => x.SoTienThanhToan),
                    tyLe = tatCaThanhToan.Count > 0
                        ? Math.Round((decimal)g.Count() * 100 / tatCaThanhToan.Count, 2)
                        : 0
                })
                .OrderByDescending(x => x.tongTien)
                .ToList();

            var bieudoTuanNay = Enumerable.Range(0, 7)
                .Select(i => homNay.AddDays(-i))
                .Select(ngay => new
                {
                    ngay = ngay.ToString("dd/MM"),
                    ngayDayDu = ngay.ToString("dd/MM/yyyy"),
                    doanhThu = tatCaThanhToan.Where(p => p.NgayThanhToan.Date == ngay).Sum(p => p.SoTienThanhToan),
                    soDon = tatCaThanhToan.Count(p => p.NgayThanhToan.Date == ngay)
                })
                .OrderBy(x => DateTime.ParseExact(x.ngayDayDu, "dd/MM/yyyy", CultureInfo.InvariantCulture))
                .ToList();

            var thongKeTrangThaiDonHang = await _context.Orders
                .GroupBy(o => o.TrangThai ?? "Không xác định")
                .Select(g => new
                {
                    trangThai = g.Key,
                    soLuong = g.Count(),
                    tongGiaTri = g.Sum(o => o.TongGiaTriDonHang)
                })
                .OrderByDescending(x => x.soLuong)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                tongQuat = new
                {
                    tongDoanhThuTatCa = tatCaThanhToan.Sum(p => p.SoTienThanhToan),
                    tongSoDon = tatCaThanhToan.Count,
                    doanhThuTrungBinh = tatCaThanhToan.Count > 0
                        ? tatCaThanhToan.Sum(p => p.SoTienThanhToan) / tatCaThanhToan.Count
                        : 0
                },
                homNay = new
                {
                    ngay = homNay.ToString("dd/MM/yyyy"),
                    tongDoanhThu = doanhThuHomNay.Sum(p => p.SoTienThanhToan),
                    soDon = doanhThuHomNay.Count
                },
                thangNay = new
                {
                    thang = thangHienTai,
                    nam = namHienTai,
                    tongDoanhThu = doanhThuThangNay.Sum(p => p.SoTienThanhToan),
                    soDon = doanhThuThangNay.Count
                },
                namNay = new
                {
                    nam = namHienTai,
                    tongDoanhThu = doanhThuNamNay.Sum(p => p.SoTienThanhToan),
                    soDon = doanhThuNamNay.Count
                },
                topKhachHang,
                thongKePhuongThuc,
                bieudoTuanNay,
                thongKeTrangThaiDonHang
            });
        }

        #endregion

        #region Thống kê đơn hàng

        [HttpGet("ThongKeDonHang")]
        public async Task<IActionResult> GetOrderStatistic()
        {
            var orders = await _context.Orders
                .Include(o => o.Payments)
                .ToListAsync();

            var thongKe = orders
                .GroupBy(o => o.TrangThai ?? "Không xác định")
                .Select(g => new
                {
                    trangThai = g.Key,
                    soLuongDon = g.Count(),
                    tongGiaTri = g.Sum(o => o.TongGiaTriDonHang),
                    daDuocThanhToan = g.Sum(o => o.Payments
                        .Where(p => IsSuccessfulPayment(p.TrangThai))
                        .Sum(p => p.SoTienThanhToan)),
                    conLai = g.Sum(o => o.TongGiaTriDonHang) - g.Sum(o => o.Payments
                        .Where(p => IsSuccessfulPayment(p.TrangThai))
                        .Sum(p => p.SoTienThanhToan))
                })
                .OrderByDescending(x => x.soLuongDon)
                .ToList();

            var tongDon = orders.Count;
            var tongGiaTriTatCaDon = orders.Sum(o => o.TongGiaTriDonHang);
            var tongDaThanhToan = orders.Sum(o => o.Payments
                .Where(p => IsSuccessfulPayment(p.TrangThai))
                .Sum(p => p.SoTienThanhToan));

            return Ok(new
            {
                success = true,
                tongDon,
                tongGiaTriTatCaDon,
                tongDaThanhToan,
                conLai = tongGiaTriTatCaDon - tongDaThanhToan,
                chiTietTheoTrangThai = thongKe
            });
        }

        #endregion
    }
}