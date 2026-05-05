using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using WebAppDoCongNghe.Service;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly DACKContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public ProductController(DACKContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        private int? GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("id")?.Value;
            if (int.TryParse(userIdClaim, out var userId) && userId > 0)
            {
                return userId;
            }
            return null;
        }

        private async Task<bool> UserPurchasedProductAsync(int userId, int productId)
        {
            return await _context.Orders
                .Include(o => o.OrderDetails)
                .AnyAsync(o =>
                    o.CustomerId == userId &&
                    (o.TrangThai == "Đã thanh toán" || o.TrangThai == "Hoàn thành") &&
                    o.OrderDetails.Any(d => d.ProductId == productId));
        }

        #region Product CRUD Operations

        [HttpGet("DanhSachSanPham")]
        public async Task<IActionResult> DanhSachSanPham()
        {
            try
            {
                var danhSachSanPham = await _context.Products
                    .Include(p => p.ProductImages)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        categoryId = p.CategoryId,
                        hinhAnh = p.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            hinhAnh = img.HinhAnh
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách sản phẩm thành công",
                    data = danhSachSanPham,
                    total = danhSachSanPham.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm",
                    error = ex.Message
                });
            }
        }

        [HttpGet("ChiTietSanPham/{id}")]
        public async Task<IActionResult> ChiTietSanPham(int id)
        {
            try
            {
                var sanPham = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (sanPham == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy sản phẩm"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy chi tiết sản phẩm thành công",
                    data = new
                    {
                        id = sanPham.Id,
                        tenSp = sanPham.TenSp,
                        moTa = sanPham.MoTa,
                        gia = sanPham.Gia,
                        soLuongConLaiTrongKho = sanPham.SoLuongConLaiTrongKho,
                        conHang = sanPham.SoLuongConLaiTrongKho > 0,
                        categoryId = sanPham.CategoryId,
                        danhMuc = sanPham.Category != null ? new
                        {
                            id = sanPham.Category.Id,
                            tenDanhMuc = sanPham.Category.TenDanhMucSp,
                            moTa = sanPham.Category.MoTaDanhMuc
                        } : null,
                        hinhAnh = sanPham.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy chi tiết sản phẩm",
                    error = ex.Message
                });
            }
        }

        [HttpPost("ThemSanPham")]
        public async Task<IActionResult> ThemSanPham([FromForm] ThemSanPhamRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ",
                    errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var sp = new Product
                {
                    TenSp = model.TenSp,
                    CategoryId = model.CategoryId,
                    MoTa = model.MoTa,
                    Gia = model.Gia,
                    SoLuongConLaiTrongKho = model.SoLuongConLaiTrongKho,
                };

                _context.Products.Add(sp);
                await _context.SaveChangesAsync();

                if (model.HinhAnh != null && model.HinhAnh.Any())
                {
                    foreach (var file in model.HinhAnh)
                    {
                        var url = await _cloudinaryService.UploadImageAsync(file, "SanPham");
                        if (!string.IsNullOrEmpty(url))
                        {
                            _context.ProductImages.Add(new ProductImage
                            {
                                ProductId = sp.Id,
                                HinhAnh = url
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await trans.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Thêm sản phẩm thành công",
                    data = new { productId = sp.Id }
                });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return BadRequest(new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi thêm sản phẩm",
                    error = ex.Message
                });
            }
        }

        [HttpPut("SuaSanPham")]
        [Authorize]
        public async Task<IActionResult> SuaSanPham([FromForm] SuaSanPhamRequest model)
        {
            // Kiểm tra quyền admin
            var userId = GetUserIdFromToken();
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không có quyền truy cập" });
            }

            var user = await _context.Customers.FirstOrDefaultAsync(c => c.Id == userId.Value);
            if (user == null || user.IsAdmin != true)
            {
                return StatusCode(403, new { success = false, message = "Chỉ admin mới có quyền sửa sản phẩm" });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                return BadRequest(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ",
                    errors = errors
                });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(r => r.Id == model.Id);

                if (product == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy sản phẩm với ID = {model.Id}"
                    });
                }

                if (!string.IsNullOrWhiteSpace(model.TenSp))
                {
                    product.TenSp = model.TenSp;
                }

                if (model.MoTa != null)
                {
                    product.MoTa = string.IsNullOrWhiteSpace(model.MoTa) ? null : model.MoTa;
                }

                if (model.Gia.HasValue)
                {
                    if (model.Gia.Value < 0)
                    {
                        return BadRequest(new { success = false, message = "Giá sản phẩm không được âm" });
                    }
                    product.Gia = model.Gia.Value;
                }

                if (model.SoLuongConLaiTrongKho.HasValue)
                {
                    if (model.SoLuongConLaiTrongKho.Value < 0)
                    {
                        return BadRequest(new { success = false, message = "Số lượng không được âm" });
                    }
                    product.SoLuongConLaiTrongKho = model.SoLuongConLaiTrongKho.Value;
                }

                if (model.CategoryId.HasValue)
                {
                    if (model.CategoryId.Value > 0)
                    {
                        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == model.CategoryId.Value);
                        if (!categoryExists)
                        {
                            return BadRequest(new { success = false, message = $"Không tìm thấy danh mục với ID = {model.CategoryId.Value}" });
                        }
                        product.CategoryId = model.CategoryId.Value;
                    }
                    else
                    {
                        product.CategoryId = null;
                    }
                }

                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                if (model.HinhAnh != null && model.HinhAnh.Any())
                {
                    foreach (var file in model.HinhAnh)
                    {
                        if (file != null && file.Length > 0)
                        {
                            try
                            {
                                var url = await _cloudinaryService.UploadImageAsync(file, "SanPham");
                                if (!string.IsNullOrEmpty(url))
                                {
                                    _context.ProductImages.Add(new ProductImage
                                    {
                                        ProductId = product.Id,
                                        HinhAnh = url
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log lỗi upload ảnh nhưng không dừng toàn bộ quá trình
                                Console.WriteLine($"Lỗi upload ảnh: {ex.Message}");
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await trans.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật sản phẩm thành công",
                    data = product
                });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi cập nhật sản phẩm",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("XoaSanPham/{id}")]
        [Authorize]
        public async Task<IActionResult> XoaSanPham(int id)
        {
            // Kiểm tra quyền admin
            var userId = GetUserIdFromToken();
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Không có quyền truy cập" });
            }

            var user = await _context.Customers.FirstOrDefaultAsync(c => c.Id == userId.Value);
            if (user == null || user.IsAdmin != true)
            {
                return StatusCode(403, new { success = false, message = "Chỉ admin mới có quyền xóa sản phẩm" });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products
                    .Include(p => p.ProductImages)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm" });
                }

                // Kiểm tra xem sản phẩm có đang được sử dụng trong đơn hàng không
                var hasOrderDetails = await _context.OrderDetails.AnyAsync(od => od.ProductId == id);
                if (hasOrderDetails)
                {
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = "Không thể xóa sản phẩm này vì đã có đơn hàng sử dụng sản phẩm này. Vui lòng xóa các đơn hàng liên quan trước." 
                    });
                }

                // Xóa các hình ảnh của sản phẩm
                if (product.ProductImages != null && product.ProductImages.Any())
                {
                    _context.ProductImages.RemoveRange(product.ProductImages);
                    await _context.SaveChangesAsync();
                }

                // Xóa các đánh giá của sản phẩm
                var reviews = await _context.ProductReviews.Where(pr => pr.ProductId == id).ToListAsync();
                if (reviews.Any())
                {
                    _context.ProductReviews.RemoveRange(reviews);
                    await _context.SaveChangesAsync();
                }

                // Xóa các khuyến mãi liên quan
                var promotions = await _context.ProductPromotions.Where(pp => pp.ProductId == id).ToListAsync();
                if (promotions.Any())
                {
                    _context.ProductPromotions.RemoveRange(promotions);
                    await _context.SaveChangesAsync();
                }

                // Xóa sản phẩm khỏi giỏ hàng
                var cartItems = await _context.ShoppingCartDetails.Where(scd => scd.ProductId == id).ToListAsync();
                if (cartItems.Any())
                {
                    _context.ShoppingCartDetails.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                }

                // Xóa sản phẩm
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                await trans.CommitAsync();

                return Ok(new { success = true, message = "Xóa sản phẩm thành công" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = "Lỗi khi xóa sản phẩm", 
                    error = ex.Message 
                });
            }
        }

        #endregion

        #region Category Operations

        [HttpGet("DanhSachDanhMuc")]
        public async Task<IActionResult> DanhSachDanhMuc()
        {
            try
            {
                var danhSachDanhMuc = await _context.Categories
                    .Select(c => new
                    {
                        id = c.Id,
                        tenDanhMuc = c.TenDanhMucSp,
                        moTa = c.MoTaDanhMuc
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách danh mục thành công",
                    data = danhSachDanhMuc,
                    total = danhSachDanhMuc.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách danh mục",
                    error = ex.Message
                });
            }
        }

        [HttpPost("ThemDanhMuc")]
        [Authorize]
        public async Task<IActionResult> ThemDanhMuc([FromBody] ThemDanhMucRequest request)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                if (string.IsNullOrWhiteSpace(request.TenDanhMuc))
                {
                    return BadRequest(new { success = false, message = "Tên danh mục không được để trống" });
                }

                // Kiểm tra trùng tên danh mục
                var danhMucTrung = await _context.Categories
                    .AnyAsync(c => c.TenDanhMucSp.ToLower().Trim() == request.TenDanhMuc.ToLower().Trim());
                
                if (danhMucTrung)
                {
                    return BadRequest(new { success = false, message = "Tên danh mục đã tồn tại" });
                }

                var danhMuc = new Category
                {
                    TenDanhMucSp = request.TenDanhMuc.Trim(),
                    MoTaDanhMuc = request.MoTaDanhMuc?.Trim()
                };

                _context.Categories.Add(danhMuc);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Thêm danh mục thành công",
                    data = new
                    {
                        id = danhMuc.Id,
                        tenDanhMuc = danhMuc.TenDanhMucSp,
                        moTa = danhMuc.MoTaDanhMuc
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi thêm danh mục",
                    error = ex.Message
                });
            }
        }

        [HttpPut("SuaDanhMuc/{id}")]
        [Authorize]
        public async Task<IActionResult> SuaDanhMuc(int id, [FromBody] SuaDanhMucRequest request)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var danhMuc = await _context.Categories.FindAsync(id);
                if (danhMuc == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy danh mục" });
                }

                if (string.IsNullOrWhiteSpace(request.TenDanhMuc))
                {
                    return BadRequest(new { success = false, message = "Tên danh mục không được để trống" });
                }

                // Kiểm tra trùng tên danh mục (trừ danh mục hiện tại)
                var danhMucTrung = await _context.Categories
                    .AnyAsync(c => c.Id != id && c.TenDanhMucSp.ToLower().Trim() == request.TenDanhMuc.ToLower().Trim());
                
                if (danhMucTrung)
                {
                    return BadRequest(new { success = false, message = "Tên danh mục đã tồn tại" });
                }

                danhMuc.TenDanhMucSp = request.TenDanhMuc.Trim();
                if (request.MoTaDanhMuc != null)
                {
                    danhMuc.MoTaDanhMuc = request.MoTaDanhMuc.Trim();
                }

                _context.Categories.Update(danhMuc);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Sửa danh mục thành công",
                    data = new
                    {
                        id = danhMuc.Id,
                        tenDanhMuc = danhMuc.TenDanhMucSp,
                        moTa = danhMuc.MoTaDanhMuc
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi sửa danh mục",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("XoaDanhMuc/{id}")]
        [Authorize]
        public async Task<IActionResult> XoaDanhMuc(int id)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var danhMuc = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (danhMuc == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy danh mục" });
                }

                // Kiểm tra xem có sản phẩm nào đang sử dụng danh mục này không
                if (danhMuc.Products != null && danhMuc.Products.Any())
                {
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = $"Không thể xóa danh mục này vì có {danhMuc.Products.Count} sản phẩm đang sử dụng" 
                    });
                }

                _context.Categories.Remove(danhMuc);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa danh mục thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi xóa danh mục",
                    error = ex.Message
                });
            }
        }

        [HttpGet("DanhSachSanPhamTheoDanhMuc/{categoryId}")]
        public async Task<IActionResult> DanhSachSanPhamTheoDanhMuc(int categoryId)
        {
            try
            {
                var sanPhamTheoDanhMuc = await _context.Products
                    .Where(p => p.CategoryId == categoryId)
                    .Include(p => p.ProductImages)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        categoryId = p.CategoryId,
                        hinhAnh = p.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            hinhAnh = img.HinhAnh
                        }).ToList()
                    })
                    .ToListAsync();

                if (sanPhamTheoDanhMuc == null || !sanPhamTheoDanhMuc.Any())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy sản phẩm nào thuộc danh mục này"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách sản phẩm theo danh mục thành công",
                    data = sanPhamTheoDanhMuc,
                    total = sanPhamTheoDanhMuc.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm theo danh mục",
                    error = ex.Message
                });
            }
        }

        #endregion

        #region Image Operations

        [HttpGet("LayDanhSachHinhAnh/{productId}")]
        public async Task<IActionResult> LayDanhSachHinhAnh(int productId)
        {
            try
            {
                var images = await _context.ProductImages
                    .Where(x => x.ProductId == productId)
                    .Select(x => new { x.Id, x.HinhAnh })
                    .ToListAsync();

                return Ok(new { success = true, data = images });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("XoaHinhAnh/{imageId}")]
        public async Task<IActionResult> XoaHinhAnh(int imageId)
        {
            try
            {
                var image = await _context.ProductImages.FindAsync(imageId);
                if (image == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy hình ảnh" });
                }

                _context.ProductImages.Remove(image);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa hình ảnh thành công" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Filter and Search Operations

        [HttpGet("KhoangGia")]
        public async Task<IActionResult> KhoangGia()
        {
            try
            {
                var sanPham = _context.Products.AsQueryable();

                if (!await sanPham.AnyAsync())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Chưa có sản phẩm nào",
                        data = new
                        {
                            giaMin = 0,
                            giaMax = 0
                        }
                    });
                }

                var giaMin = await sanPham.MinAsync(p => p.Gia);
                var giaMax = await sanPham.MaxAsync(p => p.Gia);

                return Ok(new
                {
                    success = true,
                    message = "Lấy khoảng giá thành công",
                    data = new
                    {
                        giaMin = giaMin,
                        giaMax = giaMax
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy khoảng giá",
                    error = ex.Message
                });
            }
        }

        [HttpGet("LocSanPhamTheoGia")]
        public async Task<IActionResult> LocSanPhamTheoGia(
            [FromQuery] decimal? giaMin, 
            [FromQuery] decimal? giaMax,
            [FromQuery] int? categoryId)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .AsQueryable();

                if (giaMin.HasValue)
                {
                    query = query.Where(p => p.Gia >= giaMin.Value);
                }

                if (giaMax.HasValue)
                {
                    query = query.Where(p => p.Gia <= giaMax.Value);
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                var danhSachSanPham = await query
                    .OrderBy(p => p.Gia)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        conHang = p.SoLuongConLaiTrongKho > 0,
                        categoryId = p.CategoryId,
                        danhMuc = p.Category != null ? new
                        {
                            id = p.Category.Id,
                            tenDanhMuc = p.Category.TenDanhMucSp
                        } : null,
                        hinhAnh = p.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lọc sản phẩm theo giá thành công",
                    filters = new
                    {
                        giaMin = giaMin,
                        giaMax = giaMax,
                        categoryId = categoryId
                    },
                    total = danhSachSanPham.Count,
                    data = danhSachSanPham
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lọc sản phẩm theo giá",
                    error = ex.Message
                });
            }
        }

        [HttpGet("LocVaTimKiem")]
        public async Task<IActionResult> LocVaTimKiem(
            [FromQuery] string? keyword,
            [FromQuery] int? categoryId,
            [FromQuery] decimal? giaMin,
            [FromQuery] decimal? giaMax,
            [FromQuery] string? sapXep = "gia-tang",
            [FromQuery] bool? conHang = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 12)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(keyword))
                {
                    keyword = keyword.ToLower().Trim();
                    query = query.Where(p => p.TenSp.ToLower().Contains(keyword)
                        || (p.MoTa != null && p.MoTa.ToLower().Contains(keyword)));
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                if (giaMin.HasValue)
                {
                    query = query.Where(p => p.Gia >= giaMin.Value);
                }

                if (giaMax.HasValue)
                {
                    query = query.Where(p => p.Gia <= giaMax.Value);
                }

                if (conHang.HasValue)
                {
                    query = conHang.Value 
                        ? query.Where(p => p.SoLuongConLaiTrongKho > 0)
                        : query.Where(p => p.SoLuongConLaiTrongKho == 0);
                }

                query = sapXep?.ToLower() switch
                {
                    "gia-giam" => query.OrderByDescending(p => p.Gia),
                    "ten-az" => query.OrderBy(p => p.TenSp),
                    "ten-za" => query.OrderByDescending(p => p.TenSp),
                    "moi-nhat" => query.OrderByDescending(p => p.Id),
                    _ => query.OrderBy(p => p.Gia)
                };

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)limit);

                var danhSachSanPham = await query
                    .Skip((page - 1) * limit)
                    .Take(limit)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        conHang = p.SoLuongConLaiTrongKho > 0,
                        categoryId = p.CategoryId,
                        danhMuc = p.Category != null ? new
                        {
                            id = p.Category.Id,
                            tenDanhMuc = p.Category.TenDanhMucSp
                        } : null,
                        hinhAnh = p.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lọc và tìm kiếm sản phẩm thành công",
                    filters = new
                    {
                        keyword = keyword,
                        categoryId = categoryId,
                        giaMin = giaMin,
                        giaMax = giaMax,
                        sapXep = sapXep,
                        conHang = conHang
                    },
                    total = totalItems,
                    page = page,
                    totalPages = totalPages,
                    limit = limit,
                    data = danhSachSanPham
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lọc và tìm kiếm sản phẩm",
                    error = ex.Message
                });
            }
        }

        #endregion

        [HttpGet("SanPhamNgauNhien/{count}")]
        public async Task<IActionResult> SanPhamNgauNhien(int count)
        {
            try
            {
                var sanPhamNgauNhien = await _context.Products
                    .OrderBy(x => Guid.NewGuid())
                    .Take(count)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        conHang = p.SoLuongConLaiTrongKho > 0,
                        categoryId = p.CategoryId,
                        hinhAnh = p.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy sản phẩm ngẫu nhiên thành công",
                    data = sanPhamNgauNhien
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy sản phẩm ngẫu nhiên",
                    error = ex.Message
                });
            }
        }

        #region Product Reviews

        [HttpGet("Review/{productId:int}")]
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            try
            {
                var productExists = await _context.Products.AnyAsync(p => p.Id == productId);
                if (!productExists)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm." });
                }

                var reviews = await _context.ProductReviews
                    .Include(r => r.Customer)
                    .Where(r => r.ProductId == productId)
                    .OrderByDescending(r => r.NgayDg)
                    .Select(r => new ProductReviewResponse
                    {
                        Id = r.Id,
                        ProductId = r.ProductId ?? 0,
                        CustomerId = r.CustomerId ?? 0,
                        CustomerName = r.Customer != null ? r.Customer.HoTen : "Ẩn danh",
                        Score = r.DiemDg ?? 0,
                        Content = r.NoiDungDg,
                        CreatedAt = r.NgayDg
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = reviews, total = reviews.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi tải danh sách đánh giá.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("Review/Eligibility/{productId:int}")]
        public async Task<IActionResult> GetReviewEligibility(int productId)
        {
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var existingReview = await _context.ProductReviews
                    .Where(r => r.ProductId == productId && r.CustomerId == userId)
                    .Select(r => new ProductReviewResponse
                    {
                        Id = r.Id,
                        ProductId = r.ProductId ?? 0,
                        CustomerId = r.CustomerId ?? 0,
                        CustomerName = null,
                        Score = r.DiemDg ?? 0,
                        Content = r.NoiDungDg,
                        CreatedAt = r.NgayDg
                    })
                    .FirstOrDefaultAsync();

                var canReview = await UserPurchasedProductAsync(userId.Value, productId);

                var message = canReview
                    ? null
                    : "Bạn cần mua và thanh toán sản phẩm này để có thể đánh giá.";

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        canReview,
                        review = existingReview,
                        message
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi kiểm tra quyền đánh giá.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("Review/{productId:int}")]
        public async Task<IActionResult> CreateReview(int productId, [FromBody] ProductReviewRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm." });
                }

                var hasPurchased = await UserPurchasedProductAsync(userId.Value, productId);
                if (!hasPurchased)
                {
                    return StatusCode(403, new { success = false, message = "Bạn cần mua và thanh toán sản phẩm này trước khi đánh giá." });
                }

                var existingReview = await _context.ProductReviews
                    .FirstOrDefaultAsync(r => r.CustomerId == userId && r.ProductId == productId);
                if (existingReview != null)
                {
                    return BadRequest(new { success = false, message = "Bạn đã đánh giá sản phẩm này. Vui lòng chỉnh sửa đánh giá hiện tại." });
                }

                var review = new ProductReview
                {
                    ProductId = productId,
                    CustomerId = userId,
                    DiemDg = request.Score,
                    NoiDungDg = request.Content,
                    NgayDg = DateTime.UtcNow
                };

                _context.ProductReviews.Add(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Đã gửi đánh giá thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi gửi đánh giá.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpPut("Review/{reviewId:int}")]
        public async Task<IActionResult> UpdateReview(int reviewId, [FromBody] ProductReviewRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ.", errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)) });
            }

            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var review = await _context.ProductReviews.FirstOrDefaultAsync(r => r.Id == reviewId);
                if (review == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đánh giá." });
                }

                if (review.CustomerId != userId)
                {
                    return StatusCode(403, new { success = false, message = "Bạn không thể chỉnh sửa đánh giá của người khác." });
                }

                review.DiemDg = request.Score;
                review.NoiDungDg = request.Content;
                review.NgayDg = DateTime.UtcNow;

                _context.ProductReviews.Update(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Đã cập nhật đánh giá thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi cập nhật đánh giá.", error = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("Review/{reviewId:int}")]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var review = await _context.ProductReviews.FirstOrDefaultAsync(r => r.Id == reviewId);
                if (review == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đánh giá." });
                }

                if (review.CustomerId != userId)
                {
                    return StatusCode(403, new { success = false, message = "Bạn không thể xóa đánh giá của người khác." });
                }

                _context.ProductReviews.Remove(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Đã xóa đánh giá." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa đánh giá.", error = ex.Message });
            }
        }

        #endregion

        #region Favorite Products (SẢN PHẨM YÊU THÍCH - OPTIMIZED)

        [Authorize]
        [HttpGet("YeuThich")]
        public async Task<IActionResult> LayDanhSachYeuThich()
        {
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var danhSach = await _context.SanPhamYeuThiches
                    .Where(x => x.IdCustomer == userId)
                    .Where(x => x.IdProductNavigation != null)
                    .Include(x => x.IdProductNavigation)
                        .ThenInclude(p => p!.ProductImages)
                    .OrderByDescending(x => x.Id)
                    .Select(x => new SanPhamYeuThichResponse
                    {
                        Id = x.Id,
                        IdCustomer = x.IdCustomer ?? 0,
                        IdProduct = x.IdProduct ?? 0,
                        TenSp = x.IdProductNavigation!.TenSp,
                        Gia = x.IdProductNavigation.Gia,
                        MoTa = x.IdProductNavigation.MoTa,
                        SoLuongConLai = x.IdProductNavigation.SoLuongConLaiTrongKho,
                        HinhAnhDaiDien = x.IdProductNavigation.ProductImages
                            .OrderBy(img => img.Id)
                            .Select(img => img.HinhAnh)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách yêu thích thành công",
                    total = danhSach.Count,
                    data = danhSach
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm yêu thích",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("YeuThich/{productId:int}")]
        public async Task<IActionResult> ThemYeuThich(int productId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm." });
                }

                var existing = await _context.SanPhamYeuThiches
                    .FirstOrDefaultAsync(x => x.IdCustomer == userId && x.IdProduct == productId);

                if (existing != null)
                {
                    return Ok(new { success = true, message = "Sản phẩm đã có trong danh sách yêu thích." });
                }

                var yeuThich = new SanPhamYeuThich
                {
                    IdCustomer = userId,
                    IdProduct = productId
                };

                _context.SanPhamYeuThiches.Add(yeuThich);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Đã thêm vào danh sách yêu thích.",
                    data = new { favoriteId = yeuThich.Id }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi thêm sản phẩm yêu thích",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpDelete("YeuThich/{productId:int}")]
        public async Task<IActionResult> XoaYeuThich(int productId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var yeuThich = await _context.SanPhamYeuThiches
                    .FirstOrDefaultAsync(x => x.IdCustomer == userId && x.IdProduct == productId);

                if (yeuThich == null)
                {
                    return NotFound(new { success = false, message = "Sản phẩm không có trong danh sách yêu thích." });
                }

                _context.SanPhamYeuThiches.Remove(yeuThich);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Đã xóa khỏi danh sách yêu thích." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi xóa sản phẩm yêu thích",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpPost("YeuThich/Toggle/{productId:int}")]
        public async Task<IActionResult> ToggleYeuThich(int productId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm." });
                }

                var existing = await _context.SanPhamYeuThiches
                    .FirstOrDefaultAsync(x => x.IdCustomer == userId && x.IdProduct == productId);

                bool isFavorite;
                if (existing != null)
                {
                    _context.SanPhamYeuThiches.Remove(existing);
                    isFavorite = false;
                }
                else
                {
                    var yeuThich = new SanPhamYeuThich
                    {
                        IdCustomer = userId,
                        IdProduct = productId
                    };
                    _context.SanPhamYeuThiches.Add(yeuThich);
                    isFavorite = true;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = isFavorite ? "Đã thêm vào danh sách yêu thích." : "Đã xóa khỏi danh sách yêu thích.",
                    isFavorite
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi cập nhật danh sách yêu thích",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpGet("YeuThich/TrangThai/{productId:int}")]
        public async Task<IActionResult> KiemTraYeuThich(int productId)
        {
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var isYeuThich = await _context.SanPhamYeuThiches
                    .AnyAsync(x => x.IdCustomer == userId && x.IdProduct == productId);

                return Ok(new { success = true, isFavorite = isYeuThich });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi kiểm tra trạng thái yêu thích",
                    error = ex.Message
                });
            }
        }

        [Authorize]
        [HttpGet("YeuThich/Count")]
        public async Task<IActionResult> DemSanPhamYeuThich()
        {
            try
            {
                var userId = GetUserIdFromToken();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { success = false, message = "Không thể xác định người dùng từ token." });
                }

                var count = await _context.SanPhamYeuThiches
                    .CountAsync(x => x.IdCustomer == userId);

                return Ok(new { success = true, count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Có lỗi xảy ra khi đếm sản phẩm yêu thích",
                    error = ex.Message
                });
            }
        }

        #endregion
    }

    #region Request/Response Models

    public class ThemSanPhamRequest
    {
        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
        public string TenSp { get; set; } = null!;

        public string? MoTa { get; set; }

        [Required(ErrorMessage = "Giá là bắt buộc")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Gia { get; set; }

        [Required(ErrorMessage = "Số lượng là bắt buộc")]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 0")]
        public int SoLuongConLaiTrongKho { get; set; }

        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ít nhất 1 hình ảnh")]
        public List<IFormFile> HinhAnh { get; set; } = new();
    }

    public class SuaSanPhamRequest
    {
        [Required(ErrorMessage = "ID sản phẩm là bắt buộc")]
        public int Id { get; set; }

        public string? TenSp { get; set; }

        public string? MoTa { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal? Gia { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 0")]
        public int? SoLuongConLaiTrongKho { get; set; }

        public int? CategoryId { get; set; }

        public List<IFormFile>? HinhAnh { get; set; }
    }

    public class SanPhamYeuThichResponse
    {
        public int Id { get; set; }
        public int IdCustomer { get; set; }
        public int IdProduct { get; set; }
        public string TenSp { get; set; } = string.Empty;
        public decimal? Gia { get; set; }
        public string? HinhAnhDaiDien { get; set; }
        public string? MoTa { get; set; }
        public int? SoLuongConLai { get; set; }
    }

    public class ProductReviewRequest
    {
        [Range(1, 5, ErrorMessage = "Điểm đánh giá phải nằm trong khoảng 1-5.")]
        public int Score { get; set; }

        [StringLength(1000, ErrorMessage = "Nội dung đánh giá tối đa 1000 ký tự.")]
        public string? Content { get; set; }
    }

    public class ProductReviewResponse
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int Score { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // DTOs for Category operations
    public class ThemDanhMucRequest
    {
        [Required(ErrorMessage = "Tên danh mục là bắt buộc")]
        public string TenDanhMuc { get; set; } = string.Empty;
        public string? MoTaDanhMuc { get; set; }
    }

    public class SuaDanhMucRequest
    {
        [Required(ErrorMessage = "Tên danh mục là bắt buộc")]
        public string TenDanhMuc { get; set; } = string.Empty;
        public string? MoTaDanhMuc { get; set; }
    }

    #endregion
}
