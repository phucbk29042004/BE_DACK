using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using static BE_DACK.Controllers.CustomerController;
using System.Net.Mail;
using System.Net;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContractController : ControllerBase
    {
        private readonly DACKContext _context;
        private readonly IConfiguration _configuration;
        public ContractController(DACKContext dACKContext, IConfiguration configuration)
        {
            _context = dACKContext;
            _configuration = configuration;
        }
        [HttpPost("CreateContract")]
        public IActionResult CreateContract([FromBody] Contract_Model contract)
        {
            try
            {
                var newContract = new LienHe
                {
                    FullName = contract.FullName,
                    Email = contract.Email,
                    Message = contract.Message,
                    NgayGui = DateTime.UtcNow
                };
                _context.LienHes.Add(newContract);
                _context.SaveChanges();
                var emailRequest = new GuiEmailRequest
                {
                    Email = contract.Email,
                    TieuDe = "Xác nhận liên hệ",
                    NoiDung = $"<p>Xin chào {contract.FullName},</p><p>Cảm ơn bạn đã liên hệ với chúng tôi. Chúng tôi sẽ phản hồi bạn trong thời gian sớm nhất.</p><p>Trân trọng,</p><p>Đội ngũ hỗ trợ khách hàng</p>"
                };
                var emailResult = GuiEmailInternal(emailRequest).Result;
                return Ok(new { message = "Thành công , tôi sẽ liên hệ với bạn sau nhé" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the contract", error = ex.Message });
            }
        }
        private async Task<IActionResult> GuiEmailInternal(GuiEmailRequest request)
        {
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
        private class EmailSetting
        {
            public string SmtpServer { get; set; } = string.Empty;
            public int SmtpPort { get; set; }
            public string SmtpUsername { get; set; } = string.Empty;
            public string SmtpPassword { get; set; } = string.Empty;
            public string SenderEmail { get; set; } = string.Empty;
            public string? SenderName { get; set; }
        }

    }
}
