using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromotionController : ControllerBase
    {
        private readonly DACKContext _context;

        public PromotionController(DACKContext context)
        {
            _context = context;
        }

        [HttpGet("DanhSachKhuyenMai")]
        public IActionResult DanhSachKhuyenMai()
        {
            try
            {
                var promotions = _context.Promotions
                    .Include(p => p.ProductPromotions)
                    .ThenInclude(pp => pp.Product)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenKhuyenMai = p.TenKhuyenMai,
                        moTa = p.MoTa,
                        phanTramGiam = p.PhanTramGiam,
                        ngayBatDau = p.NgayBatDau,
                        ngayKetThuc = p.NgayKetThuc,
                        soSanPham = p.ProductPromotions.Count,
                        trangThai = p.NgayKetThuc >= DateOnly.FromDateTime(DateTime.Now) &&
                                   p.NgayBatDau <= DateOnly.FromDateTime(DateTime.Now) ? "Đang áp dụng" :
                                   p.NgayBatDau > DateOnly.FromDateTime(DateTime.Now) ? "Sắp diễn ra" : "Đã kết thúc"
                    })
                    .OrderByDescending(p => p.ngayBatDau)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = promotions,
                    total = promotions.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách khuyến mãi",
                    error = ex.Message
                });
            }
        }

        [HttpGet("KhuyenMaiDangApDung")]
        public IActionResult KhuyenMaiDangApDung()
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);

                var activePromotions = _context.Promotions
                    .Where(p => p.NgayBatDau <= today && p.NgayKetThuc >= today)
                    .Include(p => p.ProductPromotions)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenKhuyenMai = p.TenKhuyenMai,
                        moTa = p.MoTa,
                        phanTramGiam = p.PhanTramGiam,
                        ngayBatDau = p.NgayBatDau,
                        ngayKetThuc = p.NgayKetThuc,
                        soSanPham = p.ProductPromotions.Count
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = activePromotions,
                    total = activePromotions.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy khuyến mãi đang áp dụng",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy chi tiết khuyến mãi theo ID
        /// </summary>
        [HttpGet("ChiTietKhuyenMai/{id}")]
        public IActionResult ChiTietKhuyenMai(int id)
        {
            try
            {
                var promotion = _context.Promotions
                    .Include(p => p.ProductPromotions)
                    .ThenInclude(pp => pp.Product)
                    .ThenInclude(p => p.ProductImages)
                    .FirstOrDefault(p => p.Id == id);

                if (promotion == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khuyến mãi" });
                }

                var result = new
                {
                    id = promotion.Id,
                    tenKhuyenMai = promotion.TenKhuyenMai,
                    moTa = promotion.MoTa,
                    phanTramGiam = promotion.PhanTramGiam,
                    ngayBatDau = promotion.NgayBatDau,
                    ngayKetThuc = promotion.NgayKetThuc,
                    sanPhams = promotion.ProductPromotions.Select(pp => new
                    {
                        id = pp.Product.Id,
                        tenSp = pp.Product.TenSp,
                        giaGoc = pp.Product.Gia,
                        giaSauGiam = pp.Product.Gia - (pp.Product.Gia * promotion.PhanTramGiam / 100),
                        hinhAnh = pp.Product.ProductImages.FirstOrDefault()?.HinhAnh
                    }).ToList()
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy chi tiết khuyến mãi",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Tạo khuyến mãi mới (Admin)
        /// </summary>
        [HttpPost("TaoKhuyenMai")]
        [Authorize] // Yêu cầu đăng nhập
        public IActionResult TaoKhuyenMai([FromBody] TaoKhuyenMaiRequest request)
        {
            try
            {
                // Kiểm tra quyền admin
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                // Validate
                if (string.IsNullOrWhiteSpace(request.TenKhuyenMai))
                {
                    return BadRequest(new { success = false, message = "Tên khuyến mãi không được để trống" });
                }

                if (request.PhanTramGiam <= 0 || request.PhanTramGiam > 100)
                {
                    return BadRequest(new { success = false, message = "Phần trăm giảm phải từ 1-100" });
                }

                if (request.NgayKetThuc <= request.NgayBatDau)
                {
                    return BadRequest(new { success = false, message = "Ngày kết thúc phải sau ngày bắt đầu" });
                }

                var promotion = new Promotion
                {
                    TenKhuyenMai = request.TenKhuyenMai,
                    MoTa = request.MoTa,
                    PhanTramGiam = request.PhanTramGiam,
                    NgayBatDau = request.NgayBatDau,
                    NgayKetThuc = request.NgayKetThuc
                };

                _context.Promotions.Add(promotion);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Tạo khuyến mãi thành công",
                    data = new
                    {
                        id = promotion.Id,
                        tenKhuyenMai = promotion.TenKhuyenMai,
                        phanTramGiam = promotion.PhanTramGiam
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tạo khuyến mãi",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Cập nhật khuyến mãi (Admin)
        /// </summary>
        [HttpPut("CapNhatKhuyenMai/{id}")]
        [Authorize]
        public IActionResult CapNhatKhuyenMai(int id, [FromBody] TaoKhuyenMaiRequest request)
        {
            try
            {
                // Kiểm tra quyền admin
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                var promotion = _context.Promotions.FirstOrDefault(p => p.Id == id);
                if (promotion == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khuyến mãi" });
                }

                // Cập nhật thông tin
                if (!string.IsNullOrWhiteSpace(request.TenKhuyenMai))
                    promotion.TenKhuyenMai = request.TenKhuyenMai;

                promotion.MoTa = request.MoTa;

                if (request.PhanTramGiam > 0 && request.PhanTramGiam <= 100)
                    promotion.PhanTramGiam = request.PhanTramGiam;

                if (request.NgayBatDau != default(DateOnly))
                    promotion.NgayBatDau = request.NgayBatDau;

                if (request.NgayKetThuc != default(DateOnly))
                    promotion.NgayKetThuc = request.NgayKetThuc;

                _context.Promotions.Update(promotion);
                _context.SaveChanges();

                return Ok(new { success = true, message = "Cập nhật khuyến mãi thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi cập nhật khuyến mãi",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Xóa khuyến mãi (Admin)
        /// </summary>
        [HttpDelete("XoaKhuyenMai/{id}")]
        [Authorize]
        public IActionResult XoaKhuyenMai(int id)
        {
            try
            {
                // Kiểm tra quyền admin
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                var promotion = _context.Promotions
                    .Include(p => p.ProductPromotions)
                    .FirstOrDefault(p => p.Id == id);

                if (promotion == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khuyến mãi" });
                }

                // Xóa các ProductPromotion liên quan trước
                _context.ProductPromotions.RemoveRange(promotion.ProductPromotions);
                _context.Promotions.Remove(promotion);
                _context.SaveChanges();

                return Ok(new { success = true, message = "Xóa khuyến mãi thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi xóa khuyến mãi",
                    error = ex.Message
                });
            }
        }

        // ==================== QUẢN LÝ SẢN PHẨM KHUYẾN MÃI ====================

        /// <summary>
        /// Thêm sản phẩm vào khuyến mãi (Admin)
        /// </summary>
        [HttpPost("ThemSanPhamVaoKhuyenMai")]
        [Authorize]
        public IActionResult ThemSanPhamVaoKhuyenMai([FromBody] ThemSanPhamKhuyenMaiRequest request)
        {
            try
            {
                // Kiểm tra quyền admin
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                // Kiểm tra promotion tồn tại
                var promotion = _context.Promotions.FirstOrDefault(p => p.Id == request.PromotionId);
                if (promotion == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khuyến mãi" });
                }

                // Kiểm tra product tồn tại
                var product = _context.Products.FirstOrDefault(p => p.Id == request.ProductId);
                if (product == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm" });
                }

                // Kiểm tra đã tồn tại chưa
                var existing = _context.ProductPromotions
                    .FirstOrDefault(pp => pp.ProductId == request.ProductId && pp.PromotionId == request.PromotionId);

                if (existing != null)
                {
                    return Conflict(new { success = false, message = "Sản phẩm đã có trong khuyến mãi này" });
                }

                // Thêm mới
                var productPromotion = new ProductPromotion
                {
                    ProductId = request.ProductId,
                    PromotionId = request.PromotionId
                };

                _context.ProductPromotions.Add(productPromotion);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Thêm sản phẩm vào khuyến mãi thành công"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi thêm sản phẩm vào khuyến mãi",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Xóa sản phẩm khỏi khuyến mãi (Admin)
        /// </summary>
        [HttpDelete("XoaSanPhamKhoiKhuyenMai")]
        [Authorize]
        public IActionResult XoaSanPhamKhoiKhuyenMai([FromQuery] int productId, [FromQuery] int promotionId)
        {
            try
            {
                // Kiểm tra quyền admin
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                var productPromotion = _context.ProductPromotions
                    .FirstOrDefault(pp => pp.ProductId == productId && pp.PromotionId == promotionId);

                if (productPromotion == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm trong khuyến mãi này" });
                }

                _context.ProductPromotions.Remove(productPromotion);
                _context.SaveChanges();

                return Ok(new { success = true, message = "Xóa sản phẩm khỏi khuyến mãi thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi xóa sản phẩm khỏi khuyến mãi",
                    error = ex.Message
                });
            }
        }

        [HttpGet("SanPhamKhuyenMai")]
        public IActionResult SanPhamKhuyenMai()
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);

                var products = _context.ProductPromotions
                    .Include(pp => pp.Product)
                    .ThenInclude(p => p.ProductImages)
                    .Include(pp => pp.Promotion)
                    .Where(pp => pp.Promotion.NgayBatDau <= today && pp.Promotion.NgayKetThuc >= today)
                    .Select(pp => new
                    {
                        id = pp.Product.Id,
                        tenSp = pp.Product.TenSp,
                        moTa = pp.Product.MoTa,
                        giaGoc = pp.Product.Gia,
                        phanTramGiam = pp.Promotion.PhanTramGiam,
                        giaSauGiam = pp.Product.Gia - (pp.Product.Gia * pp.Promotion.PhanTramGiam / 100),
                        soTienGiam = pp.Product.Gia * pp.Promotion.PhanTramGiam / 100,
                        hinhAnh = pp.Product.ProductImages.FirstOrDefault().HinhAnh,
                        tenKhuyenMai = pp.Promotion.TenKhuyenMai,
                        ngayKetThuc = pp.Promotion.NgayKetThuc
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = products,
                    total = products.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm khuyến mãi",
                    error = ex.Message
                });
            }
        }

        [HttpGet("KhuyenMaiCuaSanPham/{productId}")]
        public IActionResult KhuyenMaiCuaSanPham(int productId)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);

                var productPromotion = _context.ProductPromotions
                    .Include(pp => pp.Promotion)
                    .Include(pp => pp.Product)
                    .Where(pp => pp.ProductId == productId &&
                                pp.Promotion.NgayBatDau <= today &&
                                pp.Promotion.NgayKetThuc >= today)
                    .Select(pp => new
                    {
                        promotionId = pp.Promotion.Id,
                        tenKhuyenMai = pp.Promotion.TenKhuyenMai,
                        moTa = pp.Promotion.MoTa,
                        phanTramGiam = pp.Promotion.PhanTramGiam,
                        giaGoc = pp.Product.Gia,
                        giaSauGiam = pp.Product.Gia - (pp.Product.Gia * pp.Promotion.PhanTramGiam / 100),
                        soTienGiam = pp.Product.Gia * pp.Promotion.PhanTramGiam / 100,
                        ngayBatDau = pp.Promotion.NgayBatDau,
                        ngayKetThuc = pp.Promotion.NgayKetThuc
                    })
                    .FirstOrDefault();

                if (productPromotion == null)
                {
                    return Ok(new
                    {
                        success = true,
                        hasPromotion = false,
                        message = "Sản phẩm không có khuyến mãi"
                    });
                }

                return Ok(new
                {
                    success = true,
                    hasPromotion = true,
                    data = productPromotion
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy thông tin khuyến mãi sản phẩm",
                    error = ex.Message
                });
            }
        }


        public class TaoKhuyenMaiRequest
        {
            public string TenKhuyenMai { get; set; } = null!;
            public string? MoTa { get; set; }
            public decimal PhanTramGiam { get; set; }
            public DateOnly NgayBatDau { get; set; }
            public DateOnly NgayKetThuc { get; set; }
        }

        public class ThemSanPhamKhuyenMaiRequest
        {
            public int ProductId { get; set; }
            public int PromotionId { get; set; }
        }
    }
}