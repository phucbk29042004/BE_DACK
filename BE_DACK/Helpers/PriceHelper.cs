using BE_DACK.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Helpers
{
    public static class PriceHelper
    {
        // Tính giá sau khuyến mãi cho 1 sản phẩm
        public static async Task<decimal> TinhGiaSauKhuyenMai(DACKContext context, int productId, decimal giaGoc)
        {
            // Lấy ngày hiện tại dưới dạng DateOnly
            var today = DateOnly.FromDateTime(DateTime.Now);

            // Tìm khuyến mãi đang active cho sản phẩm này
            var khuyenMai = await context.ProductPromotions
                .Include(pp => pp.Promotion)
                .Where(pp => pp.ProductId == productId
                    && pp.Promotion != null
                    && pp.Promotion.NgayBatDau <= today   
                    && pp.Promotion.NgayKetThuc >= today) 
                .Select(pp => pp.Promotion)
                .FirstOrDefaultAsync();

            // Cần check null vì PhanTramGiam là decimal?
            if (khuyenMai != null && khuyenMai.PhanTramGiam.HasValue && khuyenMai.PhanTramGiam.Value > 0)
            {
                // Áp dụng giảm giá phần trăm
                decimal giaGiam = giaGoc * khuyenMai.PhanTramGiam.Value / 100;
                return giaGoc - giaGiam;
            }

            // Không có khuyến mãi, trả về giá gốc
            return giaGoc;
        }

        // Lấy thông tin khuyến mãi đang áp dụng
        public static async Task<object?> LayThongTinKhuyenMai(DACKContext context, int productId)
        {
            // Lấy ngày hiện tại dưới dạng DateOnly
            var today = DateOnly.FromDateTime(DateTime.Now);

            var khuyenMai = await context.ProductPromotions
                .Include(pp => pp.Promotion)
                .Where(pp => pp.ProductId == productId
                    && pp.Promotion != null
                    && pp.Promotion.NgayBatDau <= today   
                    && pp.Promotion.NgayKetThuc >= today)
                .Select(pp => new
                {
                    id = pp.Promotion.Id,
                    tenKhuyenMai = pp.Promotion.TenKhuyenMai,
                    // Sửa tên thuộc tính thành PhanTramGiam cho đúng với Entity
                    giamGia = pp.Promotion.PhanTramGiam,
                    ngayBatDau = pp.Promotion.NgayBatDau,
                    ngayKetThuc = pp.Promotion.NgayKetThuc
                })
                .FirstOrDefaultAsync();

            return khuyenMai;
        }
    }
}