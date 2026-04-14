using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class Product
{
    public int Id { get; set; }

    public string TenSp { get; set; } = null!;

    public string? MoTa { get; set; }

    public decimal Gia { get; set; }

    public int SoLuongConLaiTrongKho { get; set; }

    public int? CategoryId { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductPromotion> ProductPromotions { get; set; } = new List<ProductPromotion>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<SanPhamYeuThich> SanPhamYeuThiches { get; set; } = new List<SanPhamYeuThich>();

    public virtual ICollection<ShoppingCartDetail> ShoppingCartDetails { get; set; } = new List<ShoppingCartDetail>();
}
