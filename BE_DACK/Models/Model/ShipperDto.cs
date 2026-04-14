namespace BE_DACK.Models.Model
{
    public class ShipperDto
    {
        public string TenShipper { get; set; } = null!;
        public string DienThoai { get; set; } = null!;
        public string? Email { get; set; }
        public bool? TrangThai { get; set; }
    }
}
