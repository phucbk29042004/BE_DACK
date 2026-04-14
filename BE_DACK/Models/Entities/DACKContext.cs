using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Models.Entities;

public partial class DACKContext : DbContext
{
    public DACKContext()
    {
    }

    public DACKContext(DbContextOptions<DACKContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountType> AccountTypes { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<LienHe> LienHes { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductPromotion> ProductPromotions { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<SanPhamYeuThich> SanPhamYeuThiches { get; set; }

    public virtual DbSet<Shipper> Shippers { get; set; }

    public virtual DbSet<ShoppingCart> ShoppingCarts { get; set; }

    public virtual DbSet<ShoppingCartDetail> ShoppingCartDetails { get; set; }

    public virtual DbSet<TonKhoSummary> TonKhoSummaries { get; set; }

    public virtual DbSet<ForumPost> ForumPosts { get; set; }
    public virtual DbSet<ForumComment> ForumComments { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AccountT__3214EC0756BE5181");

            entity.Property(e => e.TenLoaiTaiKhoan).HasMaxLength(100);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC07114A0B5B");

            entity.Property(e => e.TenDanhMucSp)
                .HasMaxLength(255)
                .HasColumnName("TenDanhMucSP");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC07C6BC7D87");

            entity.HasIndex(e => e.Email, "UQ__Customer__A9D105340D8ADE52").IsUnique();

            entity.Property(e => e.DiaChi).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.HoTen).HasMaxLength(255);
            entity.Property(e => e.Sdt)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SDT");

            entity.HasOne(d => d.IdAccountTypesNavigation).WithMany(p => p.Customers)
                .HasForeignKey(d => d.IdAccountTypes)
                .HasConstraintName("FK_Customers_AccountTypes");
        });

        modelBuilder.Entity<LienHe>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LienHe__3214EC078F373409");

            entity.ToTable("LienHe");

            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.NgayGui)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Orders__3214EC07F66B8408");

            entity.Property(e => e.NgayTaoDonHang).HasColumnType("datetime");
            entity.Property(e => e.TongGiaTriDonHang).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__Orders__Customer__4AB81AF0");

            entity.HasOne(d => d.IdShipperNavigation).WithMany(p => p.Orders)
                .HasForeignKey(d => d.IdShipper)
                .HasConstraintName("FK_Orders_Shipper");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OrderDet__3214EC07A6D97901");

            entity.Property(e => e.Gia).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.SoLuongSp).HasColumnName("SoLuongSP");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderDeta__Order__48CFD27E");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__OrderDeta__Produ__49C3F6B7");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payments__3214EC07409022FE");

            entity.Property(e => e.NgayThanhToan).HasColumnType("datetime");
            entity.Property(e => e.PhuongThucThanhToan).HasMaxLength(50);
            entity.Property(e => e.SoTienThanhToan).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__Payments__OrderI__4D94879B");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Products__3214EC07879CA271");

            entity.Property(e => e.Gia).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TenSp)
                .HasMaxLength(255)
                .HasColumnName("TenSP");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Products__Catego__534D60F1");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductI__3214EC073DF9C3F4");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProductIm__Produ__4E88ABD4");
        });

        modelBuilder.Entity<ProductPromotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductP__3214EC07EC8B8633");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductPromotions)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ProductPr__Produ__4F7CD00D");

            entity.HasOne(d => d.Promotion).WithMany(p => p.ProductPromotions)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("FK__ProductPr__Promo__5070F446");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductR__3214EC07450A8A3E");

            entity.Property(e => e.DiemDg).HasColumnName("DiemDG");
            entity.Property(e => e.NgayDg)
                .HasColumnType("datetime")
                .HasColumnName("NgayDG");
            entity.Property(e => e.NoiDungDg)
                .HasMaxLength(255)
                .HasColumnName("NoiDungDG");

            entity.HasOne(d => d.Customer).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__ProductRe__Custo__5165187F");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ProductRe__Produ__52593CB8");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Promotio__3214EC0782B37A8F");

            entity.Property(e => e.MoTa).HasMaxLength(255);
            entity.Property(e => e.PhanTramGiam).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.TenKhuyenMai).HasMaxLength(100);
        });

        modelBuilder.Entity<SanPhamYeuThich>(entity =>
        {
            entity.ToTable("SanPhamYeuThich");

            entity.HasOne(d => d.IdCustomerNavigation).WithMany(p => p.SanPhamYeuThiches)
                .HasForeignKey(d => d.IdCustomer)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SanPhamYeuThich_Customer");

            entity.HasOne(d => d.IdProductNavigation).WithMany(p => p.SanPhamYeuThiches)
                .HasForeignKey(d => d.IdProduct)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SanPhamYeuThich_Product");
        });

        modelBuilder.Entity<Shipper>(entity =>
        {
            entity.HasKey(e => e.ShipperId).HasName("PK__SHIPPER__1F8AFFB9F5EB9759");

            entity.ToTable("SHIPPER");

            entity.Property(e => e.ShipperId).HasColumnName("ShipperID");
            entity.Property(e => e.DienThoai)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.TenShipper).HasMaxLength(100);
        });

        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shopping__3214EC072B14F1B9");

            entity.ToTable("ShoppingCart");

            entity.HasOne(d => d.Customer).WithMany(p => p.ShoppingCarts)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__ShoppingC__Custo__5629CD9C");
        });

        modelBuilder.Entity<ShoppingCartDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shopping__3214EC07505F4970");

            entity.Property(e => e.SoLuongTrongGh).HasColumnName("SoLuongTrongGH");

            entity.HasOne(d => d.Cart).WithMany(p => p.ShoppingCartDetails)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("FK__ShoppingC__CartI__571DF1D5");

            entity.HasOne(d => d.Product).WithMany(p => p.ShoppingCartDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ShoppingC__Produ__5812160E");
        });

        modelBuilder.Entity<TonKhoSummary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TonKhoSu__3214EC07B9A266AD");

            entity.ToTable("TonKhoSummary");

            entity.Property(e => e.Dvt)
                .HasMaxLength(50)
                .HasColumnName("DVT");
            entity.Property(e => e.TenHh)
                .HasMaxLength(100)
                .HasColumnName("TenHH");
            entity.Property(e => e.TongSoLuongNhap).HasDefaultValue(0);
            entity.Property(e => e.TongSoLuongXuat).HasDefaultValue(0);
            entity.Property(e => e.TongSoTon).HasDefaultValue(0);
        });

        modelBuilder.Entity<ForumPost>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ForumPos__3214EC07");

            entity.ToTable("ForumPost");

            entity.Property(e => e.TieuDe)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.NoiDung)
                .HasColumnType("NVARCHAR(MAX)")
                .IsRequired();

            entity.Property(e => e.LuotXem)
                .HasDefaultValue(0);

            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("GETDATE()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.ForumPosts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ForumPost_Customer");
        });

        modelBuilder.Entity<ForumComment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ForumCom__3214EC07");

            entity.ToTable("ForumComment");

            entity.Property(e => e.NoiDung)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("GETDATE()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Post)
                .WithMany(p => p.ForumComments)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ForumComment_ForumPost");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.ForumComments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ForumComment_Customer");
        });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
