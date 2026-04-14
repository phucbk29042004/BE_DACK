using BE_DACK.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly DACKContext _context;
        private readonly IConfiguration _configuration;
        
        public CustomerController( DACKContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("DangKy")]
        public IActionResult DangKy([FromBody] RegisterModel customer)
        {
            try
            {
                // Validate dữ liệu đầu vào
                if (string.IsNullOrEmpty(customer.Email) || string.IsNullOrEmpty(customer.MatKhau))
                {
                    return BadRequest(new { success = false, message = "Vui lòng cung cấp đầy đủ email và mật khẩu" });
                }

                if (string.IsNullOrEmpty(customer.HoTen))
                {
                    return BadRequest(new { success = false, message = "Vui lòng cung cấp họ tên" });
                }

                // Kiểm tra email đã tồn tại
                var existingUser = _context.Customers
                    .FirstOrDefault(u => u.Email == customer.Email);

                if (existingUser != null)
                {
                    return Conflict(new { success = false, message = "Email đã được sử dụng" });
                }

                // Kiểm tra AccountType có tồn tại không
                var accountType = _context.AccountTypes
                    .FirstOrDefault(at => at.Id == 1);

                if (accountType == null)
                {
                    return StatusCode(500, new { success = false, message = "Loại tài khoản không tồn tại trong hệ thống" });
                }

                // Tạo customer mới
                var newUser = new Customer
                {
                    Email = customer.Email,
                    Sdt = customer.Sdt,
                    MatKhau = customer.MatKhau, // Lưu trực tiếp mật khẩu
                    HoTen = customer.HoTen,
                    DiaChi = customer.DiaChi,
                    IsAdmin = false,
                    IdAccountTypes = 1
                };

                _context.Customers.Add(newUser);
                _context.SaveChanges();

                // Load lại thông tin kèm AccountType
                var userWithAccountType = _context.Customers
                    .Include(c => c.IdAccountTypesNavigation)
                    .FirstOrDefault(c => c.Id == newUser.Id);

                // Trả về thông tin (không trả về mật khẩu)
                return Ok(new
                {
                    success = true,
                    message = "Đăng ký thành công",
                    user = new
                    {
                        id = userWithAccountType.Id,
                        hoTen = userWithAccountType.HoTen,
                        email = userWithAccountType.Email,
                        sdt = userWithAccountType.Sdt,
                        diaChi = userWithAccountType.DiaChi,
                        isAdmin = userWithAccountType.IsAdmin,
                        loaiTaiKhoan = userWithAccountType.IdAccountTypesNavigation?.TenLoaiTaiKhoan
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi đăng ký tài khoản",
                    error = ex.Message
                });
            }
        }

        [HttpPost("DangNhap")]
        public IActionResult DangNhap([FromBody] LoginModel model)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.MatKhau))
                {
                    return BadRequest(new { success = false, message = "Vui lòng cung cấp đầy đủ email và mật khẩu" });
                }

                // Tìm user theo email
                var customer = _context.Customers
                    .Include(c => c.IdAccountTypesNavigation)
                    .FirstOrDefault(c => c.Email == model.Email);

                if (customer == null || customer.MatKhau != model.MatKhau)
                {
                    return Unauthorized(new { success = false, message = "Email hoặc mật khẩu không chính xác" });
                }

                // Sinh JWT token
                var jwtKey = _configuration["Jwt:Key"];
                var jwtIssuer = _configuration["Jwt:Issuer"];
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                // Claims: chứa thông tin sẽ lưu trong token
                var claims = new[]
                {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Email),
            new Claim("id", customer.Id.ToString()),
            new Claim("hoTen", customer.HoTen),
            new Claim("isAdmin", customer.IsAdmin.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtIssuer,
                    claims: claims,
                    expires: DateTime.UtcNow.AddHours(3), // Token sống 3 tiếng
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                // Trả về thông tin người dùng + token
                return Ok(new
                {
                    success = true,
                    message = "Đăng nhập thành công",
                    token = tokenString,
                    user = new
                    {
                        id = customer.Id,
                        hoTen = customer.HoTen,
                        email = customer.Email,
                        sdt = customer.Sdt,
                        diaChi = customer.DiaChi,
                        isAdmin = customer.IsAdmin,
                        loaiTaiKhoan = customer.IdAccountTypesNavigation?.TenLoaiTaiKhoan
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi đăng nhập",
                    error = ex.Message
                });
            }
        }

        [HttpPost("DoiMatKhau")]
        public IActionResult DoiMatKhau([FromBody] DoiMatKhauRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.MatKhauMoi))
                {
                    return BadRequest(new { success = false, message = "Email và mật khẩu mới không được để trống." });
                }

                if (!IsValidEmail(request.Email))
                {
                    return BadRequest(new { success = false, message = "Email không hợp lệ." });
                }

                var user = _context.Customers.FirstOrDefault(u => u.Email == request.Email);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Email không tồn tại trong hệ thống." });
                }

                // Cập nhật mật khẩu mới (ở đây chưa mã hóa)
                user.MatKhau = request.MatKhauMoi;
                _context.Customers.Update(user);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Đổi mật khẩu thành công.",
                    idTaiKhoan = user.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi đổi mật khẩu.",
                    error = ex.Message
                });
            }
        }
        [HttpPost("QuenMatKhau")]
        public IActionResult QuenMatKhau([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var user = _context.Customers.FirstOrDefault(x => x.Email == request.Email);
                if (user == null)
                    return NotFound(new { success = false, message = "Email không tồn tại trong hệ thống!" });

                // Sinh mật khẩu mới ngẫu nhiên
                string newPass = Guid.NewGuid().ToString().Substring(0, 8);
                user.MatKhau = newPass;
                _context.SaveChanges();


                return Ok(new { success = true, message = "Đã gửi mật khẩu mới qua email." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xử lý yêu cầu", error = ex.Message });
            }
        }
        [HttpGet("ThongTinCaNhan")]
        [Authorize] // Yêu cầu phải có token hợp lệ
        public IActionResult ThongTinCaNhan()
        {
            try
            {
                // Lấy userId từ claims trong token
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");

                if (userIdClaim == null)
                {
                    return Unauthorized(new { success = false, message = "Token không hợp lệ" });
                }

                int userId = int.Parse(userIdClaim.Value);

                // Tìm user theo ID và load thông tin AccountType
                var customer = _context.Customers
                    .Include(c => c.IdAccountTypesNavigation)
                    .FirstOrDefault(c => c.Id == userId);

                if (customer == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Trả về thông tin (không trả mật khẩu)
                return Ok(new
                {
                    success = true,
                    message = "Lấy thông tin thành công",
                    user = new
                    {
                        id = customer.Id,
                        hoTen = customer.HoTen,
                        email = customer.Email,
                        sdt = customer.Sdt,
                        diaChi = customer.DiaChi,
                        isAdmin = customer.IsAdmin,
                        loaiTaiKhoan = customer.IdAccountTypesNavigation?.TenLoaiTaiKhoan
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy thông tin cá nhân",
                    error = ex.Message
                });
            }
        }

        [HttpGet("ThongTinNguoiDung/{id}")]
        public IActionResult ThongTinNguoiDungTheoId(int id)
        {
            try
            {
                var customer = _context.Customers
                    .Include(c => c.IdAccountTypesNavigation)
                    .FirstOrDefault(c => c.Id == id);

                if (customer == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                return Ok(new
                {
                    success = true,
                    user = new
                    {
                        id = customer.Id,
                        hoTen = customer.HoTen,
                        email = customer.Email,
                        sdt = customer.Sdt,
                        diaChi = customer.DiaChi,
                        isAdmin = customer.IsAdmin,
                        loaiTaiKhoan = customer.IdAccountTypesNavigation?.TenLoaiTaiKhoan
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy thông tin người dùng",
                    error = ex.Message
                });
            }
        }
        [HttpPut("CapNhatThongTin")]
        [Authorize]
        public IActionResult CapNhatThongTin([FromBody] CapNhatThongTinRequest request)
        {
            try
            {
                // Lấy userId từ token
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
                if (userIdClaim == null)
                {
                    return Unauthorized(new { success = false, message = "Token không hợp lệ" });
                }

                int userId = int.Parse(userIdClaim.Value);

                var customer = _context.Customers.FirstOrDefault(c => c.Id == userId);
                if (customer == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Cập nhật thông tin (chỉ những trường được gửi lên)
                if (!string.IsNullOrWhiteSpace(request.HoTen))
                    customer.HoTen = request.HoTen;

                if (!string.IsNullOrWhiteSpace(request.Sdt))
                    customer.Sdt = request.Sdt;

                if (!string.IsNullOrWhiteSpace(request.DiaChi))
                    customer.DiaChi = request.DiaChi;

                _context.Customers.Update(customer);
                _context.SaveChanges();

                // Load lại thông tin đã cập nhật
                var updatedCustomer = _context.Customers
                    .Include(c => c.IdAccountTypesNavigation)
                    .FirstOrDefault(c => c.Id == userId);

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật thông tin thành công",
                    user = new
                    {
                        id = updatedCustomer.Id,
                        hoTen = updatedCustomer.HoTen,
                        email = updatedCustomer.Email,
                        sdt = updatedCustomer.Sdt,
                        diaChi = updatedCustomer.DiaChi,
                        isAdmin = updatedCustomer.IsAdmin,
                        loaiTaiKhoan = updatedCustomer.IdAccountTypesNavigation?.TenLoaiTaiKhoan
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi cập nhật thông tin",
                    error = ex.Message
                });
            }
        }


        // Admin: Lấy danh sách tất cả người dùng
        [HttpGet("DanhSachNguoiDung")]
        [Authorize]
        public async Task<IActionResult> DanhSachNguoiDung()
        {
            try
            {
                // Kiểm tra quyền admin
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var customers = await _context.Customers
                    .Include(c => c.IdAccountTypesNavigation)
                    .Select(c => new
                    {
                        id = c.Id,
                        hoTen = c.HoTen,
                        email = c.Email,
                        sdt = c.Sdt,
                        diaChi = c.DiaChi,
                        isAdmin = c.IsAdmin,
                        loaiTaiKhoan = c.IdAccountTypesNavigation != null ? c.IdAccountTypesNavigation.TenLoaiTaiKhoan : null
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách người dùng thành công",
                    data = customers,
                    total = customers.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách người dùng",
                    error = ex.Message
                });
            }
        }

        // Admin: Tạo hoặc cập nhật người dùng
        [HttpPost("ThemNguoiDung")]
        [Authorize]
        public IActionResult ThemNguoiDung([FromBody] ThemNguoiDungRequest request)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.HoTen))
                {
                    return BadRequest(new { success = false, message = "Email và họ tên là bắt buộc" });
                }

                // Kiểm tra email đã tồn tại
                var existing = _context.Customers.FirstOrDefault(c => c.Email == request.Email);
                if (existing != null)
                {
                    return Conflict(new { success = false, message = "Email đã được sử dụng" });
                }

                var customer = new Customer
                {
                    HoTen = request.HoTen,
                    Email = request.Email,
                    Sdt = request.Sdt,
                    DiaChi = request.DiaChi,
                    MatKhau = request.MatKhau ?? "123456", // Mật khẩu mặc định
                    IsAdmin = request.IsAdmin ?? false,
                    IdAccountTypes = 1
                };

                _context.Customers.Add(customer);
                _context.SaveChanges();

                return Ok(new { success = true, message = "Thêm người dùng thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi thêm người dùng", error = ex.Message });
            }
        }

        // Admin: Cập nhật người dùng
        [HttpPut("CapNhatNguoiDung/{id}")]
        [Authorize]
        public IActionResult CapNhatNguoiDung(int id, [FromBody] CapNhatNguoiDungRequest request)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                var customer = _context.Customers.FirstOrDefault(c => c.Id == id);
                if (customer == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                if (!string.IsNullOrWhiteSpace(request.HoTen))
                    customer.HoTen = request.HoTen;
                if (!string.IsNullOrWhiteSpace(request.Sdt))
                    customer.Sdt = request.Sdt;
                if (!string.IsNullOrWhiteSpace(request.DiaChi))
                    customer.DiaChi = request.DiaChi;
                if (request.IsAdmin.HasValue)
                    customer.IsAdmin = request.IsAdmin.Value;
                if (!string.IsNullOrWhiteSpace(request.MatKhau))
                    customer.MatKhau = request.MatKhau;

                _context.Customers.Update(customer);
                _context.SaveChanges();

                return Ok(new { success = true, message = "Cập nhật người dùng thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi cập nhật người dùng", error = ex.Message });
            }
        }

        // Admin: Xóa người dùng
        [HttpDelete("XoaNguoiDung/{id}")]
        [Authorize]
        public IActionResult XoaNguoiDung(int id)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return Forbid();
                }

                var customer = _context.Customers.FirstOrDefault(c => c.Id == id);
                if (customer == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                _context.Customers.Remove(customer);
                _context.SaveChanges();

                return Ok(new { success = true, message = "Xóa người dùng thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa người dùng", error = ex.Message });
            }
        }

        [HttpPost("GuiEmail")]
        public async Task<IActionResult> GuiEmail([FromBody] GuiEmailRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.TieuDe) || string.IsNullOrWhiteSpace(request.NoiDung))
            {
                return BadRequest(new { message = "Vui lòng cung cấp đầy đủ Email, Tiêu đề và Nội dung." });
            }

            var emailSetting = _configuration.GetSection("EmailSetting").Get<EmailSetting>();
            if (emailSetting == null || string.IsNullOrWhiteSpace(emailSetting.SmtpServer) || string.IsNullOrWhiteSpace(emailSetting.SmtpUsername) || string.IsNullOrWhiteSpace(emailSetting.SmtpPassword) || string.IsNullOrWhiteSpace(emailSetting.SenderEmail))
            {
                return StatusCode(500, new { message = "Thiếu cấu hình EmailSetting trong appsettings." });
            }

            try
            {
                using var smtpClient = new SmtpClient(emailSetting.SmtpServer, emailSetting.SmtpPort)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(emailSetting.SmtpUsername, emailSetting.SmtpPassword)
                };

                var fromAddress = string.IsNullOrWhiteSpace(emailSetting.SenderName)
                    ? new MailAddress(emailSetting.SenderEmail)
                    : new MailAddress(emailSetting.SenderEmail, emailSetting.SenderName);

                var message = new MailMessage(fromAddress, new MailAddress(request.Email))
                {
                    Subject = request.TieuDe,
                    Body = request.NoiDung,
                    IsBodyHtml = true
                };

                await smtpClient.SendMailAsync(message);

                return Ok(new { message = "Gửi email thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Gửi email thất bại: {ex.Message}" });
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        public class CapNhatThongTinRequest
        {
            public string? HoTen { get; set; }
            public string? Sdt { get; set; }
            public string? DiaChi { get; set; }
        }
        public class DoiMatKhauRequest
        {
            public string Email { get; set; } = string.Empty;
            public string MatKhauMoi { get; set; } = string.Empty;
        }
        public class GuiEmailRequest
        {
            public string Email { get; set; } = string.Empty;
            public string TieuDe { get; set; } = string.Empty;
            public string NoiDung { get; set; } = string.Empty;
        }
        private class EmailSetting
        {
            public string SmtpServer { get; set; } = string.Empty;
            public int SmtpPort { get; set; }
            public string SmtpUsername { get; set; } = string.Empty;
            public string SmtpPassword { get; set; } = string.Empty;
            public string SenderEmail { get; set; } = string.Empty;
            public string? SenderName { get; set; }
        }
        public partial class LoginModel
        {
            public string Email { get; set; } = null!;
            public string MatKhau { get; set; } = null!;

        }

        public class RegisterModel
        {
            public string HoTen { get; set; } = null!;
            public string Email { get; set; } = null!;
            public string? Sdt { get; set; }
            public string? DiaChi { get; set; }
            public string MatKhau { get; set; } = null!;
        }
        public class ForgotPasswordRequest
        {
            public string Email { get; set; }
        }

        public class ThemNguoiDungRequest
        {
            public string HoTen { get; set; } = null!;
            public string Email { get; set; } = null!;
            public string? Sdt { get; set; }
            public string? DiaChi { get; set; }
            public string? MatKhau { get; set; }
            public bool? IsAdmin { get; set; }
        }

        public class CapNhatNguoiDungRequest
        {
            public string? HoTen { get; set; }
            public string? Sdt { get; set; }
            public string? DiaChi { get; set; }
            public string? MatKhau { get; set; }
            public bool? IsAdmin { get; set; }
        }

    }
    
}
