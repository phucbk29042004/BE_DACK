namespace BE_DACK.Models.Model
{
    public class ProductRequet
    {
        public int Id { get; set; }

        public string TenSp { get; set; } = null!;

        public string? MoTa { get; set; }

        public decimal Gia { get; set; }

        public int SoLuongConLaiTrongKho { get; set; }

        public int? CategoryId { get; set; }

        public List<IFormFile> HinhAnh { get; set; }


    }
}
