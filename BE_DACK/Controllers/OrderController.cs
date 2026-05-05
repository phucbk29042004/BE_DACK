using BE_DACK.Helpers;
using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly DACKContext _context;

        public OrderController(DACKContext context)
        {
            _context = context;
        }

        // Tạo đơn hàng từ giỏ hàng
        [HttpPost("TaoDonHang")]
        public async Task<IActionResult> TaoDonHang()
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Lấy userId từ token
                if (!int.TryParse(User.FindFirst("id")?.Value, out var userId) || userId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                // Lấy giỏ hàng của user
                var gioHang = await _context.ShoppingCarts
                    .Include(g => g.ShoppingCartDetails)
                        .ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(g => g.CustomerId == userId);

                // Kiểm tra giỏ hàng
                if (gioHang == null || !gioHang.ShoppingCartDetails.Any())
                {
                    return BadRequest(new { success = false, message = "Giỏ hàng trống, không thể tạo đơn hàng." });
                }

                // Kiểm tra tồn kho cho tất cả sản phẩm
                foreach (var detail in gioHang.ShoppingCartDetails)
                {
                    if (!detail.ProductId.HasValue)
                    {
                        return BadRequest(new { success = false, message = "Giỏ hàng có sản phẩm không hợp lệ." });
                    }

                    if (detail.Product == null)
                    {
                        return BadRequest(new { success = false, message = $"Sản phẩm ID {detail.ProductId} không tồn tại." });
                    }

                    var soLuong = detail.SoLuongTrongGh.GetValueOrDefault();
                    if (soLuong <= 0)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Sản phẩm '{detail.Product.TenSp}' có số lượng trong giỏ không hợp lệ."
                        });
                    }

                    if (soLuong > detail.Product.SoLuongConLaiTrongKho)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = $"Sản phẩm '{detail.Product.TenSp}' không đủ hàng trong kho. Còn lại: {detail.Product.SoLuongConLaiTrongKho}"
                        });
                    }
                }

                // Tính tổng giá trị đơn hàng SAU KHUYẾN MÃI
                decimal tongGiaTriGoc = 0;
                decimal tongGiaTriSauKhuyenMai = 0;

                foreach (var detail in gioHang.ShoppingCartDetails)
                {
                    var giaGoc = detail.Product.Gia;
                    var soLuong = detail.SoLuongTrongGh.GetValueOrDefault();
                    var giaSauKhuyenMai = await PriceHelper.TinhGiaSauKhuyenMai(_context, detail.ProductId!.Value, giaGoc);

                    tongGiaTriGoc += soLuong * giaGoc;
                    tongGiaTriSauKhuyenMai += soLuong * giaSauKhuyenMai;
                }

                // Tạo đơn hàng mới với giá SAU KHUYẾN MÃI
                var donHang = new Order
                {
                    CustomerId = userId,
                    NgayTaoDonHang = DateTime.UtcNow,
                    TongGiaTriDonHang = tongGiaTriSauKhuyenMai, // Lưu giá sau khuyến mãi

                    TrangThai = "Chờ xác nhận"
                };

                _context.Orders.Add(donHang);
                await _context.SaveChangesAsync();

                // Tạo chi tiết đơn hàng và cập nhật tồn kho
                foreach (var detail in gioHang.ShoppingCartDetails)
                {
                    var soLuong = detail.SoLuongTrongGh.GetValueOrDefault();
                    var giaSauKhuyenMai = await PriceHelper.TinhGiaSauKhuyenMai(_context, detail.ProductId!.Value, detail.Product!.Gia);

                    var orderDetail = new OrderDetail
                    {
                        OrderId = donHang.Id,
                        ProductId = detail.ProductId,
                        SoLuongSp = soLuong,
                        Gia = giaSauKhuyenMai, // Lưu giá sau khuyến mãi
                        TrangThai = "Chờ xác nhận"
                    };

                    _context.OrderDetails.Add(orderDetail);

                    // Giảm số lượng tồn kho
                    detail.Product.SoLuongConLaiTrongKho -= soLuong;
                }

                // Xóa giỏ hàng sau khi tạo đơn
                _context.ShoppingCartDetails.RemoveRange(gioHang.ShoppingCartDetails);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Tạo đơn hàng thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        ngayTao = donHang.NgayTaoDonHang,
                        tongGiaTriGoc = tongGiaTriGoc,
                        tongGiaTriSauKhuyenMai = tongGiaTriSauKhuyenMai,
                        tietKiem = tongGiaTriGoc - tongGiaTriSauKhuyenMai,
                        trangThai = donHang.TrangThai
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lưu đơn hàng vào cơ sở dữ liệu.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tạo đơn hàng.",
                    error = ex.Message
                });
            }
        }


        // Lấy chi tiết đơn hàng
        [HttpGet("LayChiTietDonHang/{orderId}")]
        public async Task<IActionResult> LayChiTietDonHang(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(o => o.Customer)
                    .Include(o => o.IdShipperNavigation)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                var chiTiet = donHang.OrderDetails.Select(d => new
                {
                    productId = d.ProductId,
                    tenSp = d.Product?.TenSp,
                    gia = d.Gia,
                    soLuong = d.SoLuongSp,
                    thanhTien = d.SoLuongSp * d.Gia,
                    hinhAnh = d.Product.ProductImages.Select(img => new
                    {
                        id = img.Id,
                        productId = img.ProductId,
                        url = img.HinhAnh
                    }).ToList(),
                    trangThai = d.TrangThai
                }).ToList();

                // Tính toán thông tin thanh toán
                var successfulStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Thành công", "Thanh toán COD", "COD", "Thanh toán thành công"
                };

                var successfulPayments = donHang.Payments
                    .Where(p => p.TrangThai != null && successfulStatuses.Contains(p.TrangThai))
                    .ToList();

                var tongDaThanhToan = successfulPayments.Sum(p => p.SoTienThanhToan);
                var conLai = donHang.TongGiaTriDonHang - tongDaThanhToan;

                // Lấy phương thức thanh toán từ giao dịch thành công gần nhất
                var phuongThucThanhToan = successfulPayments
                    .OrderByDescending(p => p.NgayThanhToan)
                    .Select(p => p.PhuongThucThanhToan)
                    .FirstOrDefault();

                // Tạo mô tả thanh toán
                string thanhToanInfo = "";
                if (tongDaThanhToan >= donHang.TongGiaTriDonHang)
                {
                    thanhToanInfo = phuongThucThanhToan != null
                        ? $"Đã thanh toán đủ ({phuongThucThanhToan})"
                        : "Đã thanh toán đủ";
                }
                else if (tongDaThanhToan > 0)
                {
                    thanhToanInfo = phuongThucThanhToan != null
                        ? $"Đã thanh toán {tongDaThanhToan:N0}đ ({phuongThucThanhToan}), còn lại {conLai:N0}đ"
                        : $"Đã thanh toán {tongDaThanhToan:N0}đ, còn lại {conLai:N0}đ";
                }
                else
                {
                    thanhToanInfo = "Chưa thanh toán";
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy chi tiết đơn hàng thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        ngayTao = donHang.NgayTaoDonHang,
                        tongGiaTri = donHang.TongGiaTriDonHang,
                        trangThai = donHang.TrangThai,
                        khachHang = new
                        {
                            hoTen = donHang.Customer?.HoTen,
                            email = donHang.Customer?.Email,
                            sdt = donHang.Customer?.Sdt,
                            diaChi = donHang.Customer?.DiaChi
                        },
                        shipper = donHang.IdShipperNavigation != null
                            ? new
                            {
                                id = donHang.IdShipperNavigation.ShipperId,
                                tenShipper = donHang.IdShipperNavigation.TenShipper,
                                dienThoai = donHang.IdShipperNavigation.DienThoai,
                                email = donHang.IdShipperNavigation.Email
                            }
                            : null,
                        sanPham = chiTiet,
                        soLuongSanPham = chiTiet.Count,
                        thanhToan = new
                        {
                            tongDaThanhToan = tongDaThanhToan,
                            conLai = conLai,
                            phuongThucThanhToan = phuongThucThanhToan,
                            thongTin = thanhToanInfo
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy chi tiết đơn hàng.",
                    error = ex.Message
                });
            }
        }

        // Lấy danh sách đơn hàng của user
        [HttpGet("LayDanhSachDonHang")]
        public async Task<IActionResult> LayDanhSachDonHang()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var danhSachDonHang = await _context.Orders
                    .Where(o => o.CustomerId == userId)
                    .OrderByDescending(o => o.NgayTaoDonHang)
                    .Select(o => new
                    {
                        orderId = o.Id,
                        ngayTao = o.NgayTaoDonHang,
                        tongGiaTri = o.TongGiaTriDonHang,
                        trangThai = o.TrangThai,
                        soLuongSanPham = o.OrderDetails.Count,

                        tenShipper = o.IdShipperNavigation != null ? o.IdShipperNavigation.TenShipper : null,

                        sanPham = o.OrderDetails.Select(d => new
                        {
                            productId = d.ProductId,
                            tenSp = d.Product != null ? d.Product.TenSp : "",
                            soLuong = d.SoLuongSp,
                            gia = d.Gia,
                            thanhTien = d.SoLuongSp * d.Gia,

                            hinhAnh = d.Product != null ? d.Product.ProductImages.Select(img => new { id = img.Id, url = img.HinhAnh }) : null
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách đơn hàng thành công.",
                    totalOrders = danhSachDonHang.Count,
                    data = danhSachDonHang
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách đơn hàng.",
                    error = ex.Message
                });
            }
        }

        // Hủy đơn hàng (chỉ khi đơn còn ở trạng thái "Chờ xác nhận")
        [HttpPut("HuyDonHang/{orderId}")]
        public async Task<IActionResult> HuyDonHang(int orderId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });

                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                if (donHang.TrangThai != "Chờ xác nhận")
                {
                    return BadRequest(new { success = false, message = "Chỉ có thể hủy đơn hàng đang ở trạng thái 'Chờ xác nhận'." });
                }

                // Hoàn trả số lượng vào kho
                foreach (var detail in donHang.OrderDetails)
                {
                    if (detail.Product != null)
                    {
                        detail.Product.SoLuongConLaiTrongKho += detail.SoLuongSp;
                    }
                }

                // Cập nhật trạng thái
                donHang.TrangThai = "Đã hủy";
                foreach (var detail in donHang.OrderDetails)
                {
                    detail.TrangThai = "Đã hủy";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Hủy đơn hàng thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        trangThai = donHang.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi hủy đơn hàng.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("DanhSachDonHangAdmin")]
        [Authorize]
        public async Task<IActionResult> DanhSachDonHangAdmin()
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var result = await _context.Orders
                    .OrderByDescending(o => o.NgayTaoDonHang)
                    .Select(o => new
                    {
                        id = o.Id,
                        customerId = o.CustomerId,
                        khachHang = o.Customer != null ? new
                        {
                            id = o.Customer.Id,
                            hoTen = o.Customer.HoTen,
                            email = o.Customer.Email,
                            sdt = o.Customer.Sdt,
                            diaChi = o.Customer.DiaChi
                        } : null,
                        ngayTao = o.NgayTaoDonHang,
                        tongGiaTri = o.TongGiaTriDonHang,
                        trangThai = o.TrangThai,
                        soLuongSanPham = o.OrderDetails.Count,
                        tenShipper = o.IdShipperNavigation != null ? o.IdShipperNavigation.TenShipper : null,
                        chiTietSanPham = o.OrderDetails.Select(d => new
                        {
                            productId = d.ProductId,
                            tenSp = d.Product != null ? d.Product.TenSp : "N/A",
                            soLuong = d.SoLuongSp,
                            gia = d.Gia,
                            thanhTien = d.SoLuongSp * d.Gia,
                            hinhAnh = d.Product != null ? d.Product.ProductImages.Select(img => new { id = img.Id, url = img.HinhAnh }) : null
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách đơn hàng thành công",
                    data = result,
                    total = result.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách đơn hàng",
                    error = ex.Message
                });
            }
        }

        // Lấy thông tin đơn hàng đầy đủ cho hóa đơn (Admin)
        [HttpGet("ChiTietDonHangChoHoaDon/{orderId}")]
        [Authorize]
        public async Task<IActionResult> ChiTietDonHangChoHoaDon(int orderId)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var donHang = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(d => d.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(o => o.Customer)
                    .Include(o => o.Payments)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (donHang == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn hàng." });
                }

                var chiTiet = donHang.OrderDetails.Select(d => new
                {
                    productId = d.ProductId,
                    tenSp = d.Product?.TenSp ?? "N/A",
                    gia = d.Gia,
                    soLuong = d.SoLuongSp,
                    thanhTien = d.SoLuongSp * d.Gia,
                    hinhAnh = (d.Product?.ProductImages ?? Enumerable.Empty<ProductImage>())
                        .Select(img => new
                        {
                            id = img.Id,
                            url = img.HinhAnh
                        }).ToList()
                }).ToList();

                // Tính toán thông tin thanh toán
                var successfulStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Thành công", "Thanh toán COD", "COD", "Thanh toán thành công"
                };

                var successfulPayments = donHang.Payments
                    .Where(p => p.TrangThai != null && successfulStatuses.Contains(p.TrangThai))
                    .OrderByDescending(p => p.NgayThanhToan)
                    .ToList();

                var tongDaThanhToan = successfulPayments.Sum(p => p.SoTienThanhToan);
                var conLai = donHang.TongGiaTriDonHang - tongDaThanhToan;

                var lichSuThanhToan = donHang.Payments
                    .OrderByDescending(p => p.NgayThanhToan)
                    .Select(p => new
                    {
                        ngayThanhToan = p.NgayThanhToan,
                        soTien = p.SoTienThanhToan,
                        phuongThuc = p.PhuongThucThanhToan,
                        trangThai = p.TrangThai
                    }).ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy thông tin đơn hàng cho hóa đơn thành công.",
                    data = new
                    {
                        orderId = donHang.Id,
                        ngayTao = donHang.NgayTaoDonHang,
                        tongGiaTri = donHang.TongGiaTriDonHang,
                        trangThai = donHang.TrangThai,
                        khachHang = new
                        {
                            hoTen = donHang.Customer?.HoTen ?? "N/A",
                            email = donHang.Customer?.Email ?? "N/A",
                            sdt = donHang.Customer?.Sdt ?? "N/A",
                            diaChi = donHang.Customer?.DiaChi ?? "N/A"
                        },
                        sanPham = chiTiet,
                        soLuongSanPham = chiTiet.Count,
                        thanhToan = new
                        {
                            tongDaThanhToan = tongDaThanhToan,
                            conLai = conLai,
                            lichSuThanhToan = lichSuThanhToan,
                            phuongThucThanhToan = successfulPayments.Count > 0 ? successfulPayments[0].PhuongThucThanhToan : null
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy thông tin đơn hàng cho hóa đơn.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("assign-shipper")]
        public async Task<IActionResult> AssignShipper(int shipper,int idOrder )
        {
            if( shipper == 0 )
            {
                return BadRequest(new
                {
                    message = "Lỗi"
                });
                
            }
            var order = await _context.Orders.FirstOrDefaultAsync(r=>r.Id == idOrder);
            if(order == null )
            {
                return BadRequest(new
                {
                    message = "Lỗi"
                });
            }
            order.IdShipper = shipper;
            order.TrangThai = "Đang chuản ";
            _context.Orders.Update(order);
            _context.SaveChanges();

            return Ok(new
            {
                message = "Thành công"
            });
        }
    }
}
