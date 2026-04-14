using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShipperController : ControllerBase
    {
        private readonly DACKContext _context;

        public ShipperController(DACKContext context)
        {
            _context = context;
        }

        [HttpGet]
        public ActionResult GetAllShippers()
        {
            try
            {
                var shippers = _context.Shippers
                    .Select(s => new
                    {
                        s.ShipperId,
                        s.TenShipper,
                        s.DienThoai,
                        s.Email,
                        s.TrangThai,
                        SoDonHang = s.Orders.Count
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách shipper thành công",
                    data = shippers,
                    total = shippers.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách shipper",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        public ActionResult GetShipperById(int id)
        {
            try
            {
                var shipper = _context.Shippers
                    .Where(s => s.ShipperId == id)
                    .Select(s => new
                    {
                        s.ShipperId,
                        s.TenShipper,
                        s.DienThoai,
                        s.Email,
                        s.TrangThai,
                        SoDonHang = s.Orders.Count
                    })
                    .FirstOrDefault();

                if (shipper == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy shipper với ID {id}"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy thông tin shipper thành công",
                    data = shipper
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy thông tin shipper",
                    error = ex.Message
                });
            }
        }

        [HttpPost]
        public ActionResult CreateShipper([FromBody] ShipperDto shipperDto)
        {
            try
            {
                // Kiểm tra dữ liệu đầu vào
                if (string.IsNullOrWhiteSpace(shipperDto.TenShipper))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tên shipper không được để trống"
                    });
                }

                if (string.IsNullOrWhiteSpace(shipperDto.DienThoai))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Điện thoại không được để trống"
                    });
                }

                // Kiểm tra số điện thoại đã tồn tại chưa
                var existingShipper = _context.Shippers
                    .FirstOrDefault(s => s.DienThoai == shipperDto.DienThoai);

                if (existingShipper != null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Số điện thoại đã được sử dụng"
                    });
                }

                // Tạo shipper mới
                var shipper = new Shipper
                {
                    TenShipper = shipperDto.TenShipper.Trim(),
                    DienThoai = shipperDto.DienThoai.Trim(),
                    Email = shipperDto.Email?.Trim(),
                    TrangThai = shipperDto.TrangThai ?? true
                };

                _context.Shippers.Add(shipper);
                _context.SaveChanges();

                return CreatedAtAction(nameof(GetShipperById), new { id = shipper.ShipperId }, new
                {
                    success = true,
                    message = "Thêm shipper thành công",
                    data = new
                    {
                        shipper.ShipperId,
                        shipper.TenShipper,
                        shipper.DienThoai,
                        shipper.Email,
                        shipper.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi thêm shipper",
                    error = ex.Message
                });
            }
        }


        [HttpPut("{id}")]
        public ActionResult<object> UpdateShipper(int id, [FromBody] ShipperDto shipperDto)
        {
            try
            {
                var shipper = _context.Shippers.Find(id);

                if (shipper == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy shipper với ID {id}"
                    });
                }

                // Kiểm tra dữ liệu đầu vào
                if (string.IsNullOrWhiteSpace(shipperDto.TenShipper))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tên shipper không được để trống"
                    });
                }

                if (string.IsNullOrWhiteSpace(shipperDto.DienThoai))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Điện thoại không được để trống"
                    });
                }

                // Kiểm tra số điện thoại trùng với shipper khác
                var duplicatePhone = _context.Shippers
                    .Any(s => s.DienThoai == shipperDto.DienThoai && s.ShipperId != id);

                if (duplicatePhone)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Số điện thoại đã được sử dụng bởi shipper khác"
                    });
                }

                // Cập nhật thông tin
                shipper.TenShipper = shipperDto.TenShipper.Trim();
                shipper.DienThoai = shipperDto.DienThoai.Trim();
                shipper.Email = shipperDto.Email?.Trim();
                shipper.TrangThai = shipperDto.TrangThai;

                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật shipper thành công",
                    data = new
                    {
                        shipper.ShipperId,
                        shipper.TenShipper,
                        shipper.DienThoai,
                        shipper.Email,
                        shipper.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi cập nhật shipper",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public ActionResult<object> DeleteShipper(int id)
        {
            try
            {
                var shipper = _context.Shippers
                    .Include(s => s.Orders)
                    .FirstOrDefault(s => s.ShipperId == id);

                if (shipper == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy shipper với ID {id}"
                    });
                }

                // Kiểm tra xem shipper có đơn hàng không
                if (shipper.Orders.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Không thể xóa shipper đang có đơn hàng",
                        soDonHang = shipper.Orders.Count
                    });
                }

                _context.Shippers.Remove(shipper);
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Xóa shipper thành công",
                    data = new
                    {
                        shipperId = id,
                        tenShipper = shipper.TenShipper
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi xóa shipper",
                    error = ex.Message
                });
            }
        }

        [HttpPatch("{id}/toggle-status")]
        public ActionResult<object> ToggleShipperStatus(int id)
        {
            try
            {
                var shipper = _context.Shippers.Find(id);

                if (shipper == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy shipper với ID {id}"
                    });
                }

                shipper.TrangThai = !shipper.TrangThai;
                _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = $"Đã {(shipper.TrangThai == true ? "kích hoạt" : "vô hiệu hóa")} shipper",
                    data = new
                    {
                        shipper.ShipperId,
                        shipper.TenShipper,
                        shipper.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi cập nhật trạng thái",
                    error = ex.Message
                });
            }
        }

    }
}
