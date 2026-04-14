using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class Customer
{
    public int Id { get; set; }

    public string HoTen { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Sdt { get; set; }

    public string? DiaChi { get; set; }

    public string MatKhau { get; set; } = null!;

    public bool? IsAdmin { get; set; }

    public int? IdAccountTypes { get; set; }

    public virtual AccountType? IdAccountTypesNavigation { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<SanPhamYeuThich> SanPhamYeuThiches { get; set; } = new List<SanPhamYeuThich>();

    public virtual ICollection<ShoppingCart> ShoppingCarts { get; set; } = new List<ShoppingCart>();

    public virtual ICollection<ForumPost> ForumPosts { get; set; } = new List<ForumPost>();
    public virtual ICollection<ForumComment> ForumComments { get; set; } = new List<ForumComment>();
}
