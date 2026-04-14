using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class Promotion
{
    public int Id { get; set; }

    public string? TenKhuyenMai { get; set; }

    public string? MoTa { get; set; }

    public decimal? PhanTramGiam { get; set; }

    public DateOnly? NgayBatDau { get; set; }

    public DateOnly? NgayKetThuc { get; set; }

    public virtual ICollection<ProductPromotion> ProductPromotions { get; set; } = new List<ProductPromotion>();
}
