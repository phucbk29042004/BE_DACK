using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Models.Entities;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountType> AccountTypes { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<ForumComment> ForumComments { get; set; }

    public virtual DbSet<ForumPost> ForumPosts { get; set; }

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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Name=ConnectionStrings:Connection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("AccountTypes_pkey");

            entity.Property(e => e.TenLoaiTaiKhoan).HasMaxLength(100);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Categories_pkey");

            entity.Property(e => e.TenDanhMucSp)
                .HasMaxLength(255)
                .HasColumnName("TenDanhMucSP");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Customers_pkey");

            entity.HasIndex(e => e.Email, "UQ_Customers_Email").IsUnique();

            entity.Property(e => e.DiaChi).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.HoTen).HasMaxLength(255);
            entity.Property(e => e.Sdt)
                .HasMaxLength(20)
                .HasColumnName("SDT");

            entity.HasOne(d => d.IdAccountTypesNavigation).WithMany(p => p.Customers)
                .HasForeignKey(d => d.IdAccountTypes)
                .HasConstraintName("FK_Customers_AccountTypes");
        });

        modelBuilder.Entity<ForumComment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ForumComment_pkey");

            entity.ToTable("ForumComment");

            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.NoiDung).HasMaxLength(500);

            entity.HasOne(d => d.Customer).WithMany(p => p.ForumComments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ForumComment_CustomerId_fkey");

            entity.HasOne(d => d.Post).WithMany(p => p.ForumComments)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("ForumComment_PostId_fkey");
        });

        modelBuilder.Entity<ForumPost>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ForumPost_pkey");

            entity.ToTable("ForumPost");

            entity.Property(e => e.LuotXem).HasDefaultValue(0);
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.TieuDe).HasMaxLength(255);

            entity.HasOne(d => d.Customer).WithMany(p => p.ForumPosts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ForumPost_CustomerId_fkey");
        });

        modelBuilder.Entity<LienHe>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LienHe_pkey");

            entity.ToTable("LienHe");

            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.NgayGui)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Orders_pkey");

            entity.Property(e => e.NgayTaoDonHang).HasColumnType("timestamp without time zone");
            entity.Property(e => e.TongGiaTriDonHang).HasPrecision(10, 2);
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("Orders_CustomerId_fkey");

            entity.HasOne(d => d.IdShipperNavigation).WithMany(p => p.Orders)
                .HasForeignKey(d => d.IdShipper)
                .HasConstraintName("FK_Orders_Shipper");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("OrderDetails_pkey");

            entity.Property(e => e.Gia).HasPrecision(10, 2);
            entity.Property(e => e.SoLuongSp).HasColumnName("SoLuongSP");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("OrderDetails_OrderId_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("OrderDetails_ProductId_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Payments_pkey");

            entity.Property(e => e.NgayThanhToan).HasColumnType("timestamp without time zone");
            entity.Property(e => e.PhuongThucThanhToan).HasMaxLength(50);
            entity.Property(e => e.SoTienThanhToan).HasPrecision(10, 2);
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("Payments_OrderId_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Products_pkey");

            entity.Property(e => e.Gia).HasPrecision(10, 2);
            entity.Property(e => e.TenSp)
                .HasMaxLength(255)
                .HasColumnName("TenSP");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("Products_CategoryId_fkey");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProductImages_pkey");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProductImages_ProductId_fkey");
        });

        modelBuilder.Entity<ProductPromotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProductPromotions_pkey");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductPromotions)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("ProductPromotions_ProductId_fkey");

            entity.HasOne(d => d.Promotion).WithMany(p => p.ProductPromotions)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("ProductPromotions_PromotionId_fkey");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ProductReviews_pkey");

            entity.Property(e => e.DiemDg).HasColumnName("DiemDG");
            entity.Property(e => e.NgayDg)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("NgayDG");
            entity.Property(e => e.NoiDungDg)
                .HasMaxLength(255)
                .HasColumnName("NoiDungDG");

            entity.HasOne(d => d.Customer).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("ProductReviews_CustomerId_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("ProductReviews_ProductId_fkey");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Promotions_pkey");

            entity.Property(e => e.MoTa).HasMaxLength(255);
            entity.Property(e => e.PhanTramGiam).HasPrecision(5, 2);
            entity.Property(e => e.TenKhuyenMai).HasMaxLength(100);
        });

        modelBuilder.Entity<SanPhamYeuThich>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SanPhamYeuThich_pkey");

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
            entity.HasKey(e => e.ShipperId).HasName("SHIPPER_pkey");

            entity.ToTable("SHIPPER");

            entity.Property(e => e.ShipperId).HasColumnName("ShipperID");
            entity.Property(e => e.DienThoai).HasMaxLength(15);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.TenShipper).HasMaxLength(100);
        });

        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ShoppingCart_pkey");

            entity.ToTable("ShoppingCart");

            entity.HasOne(d => d.Customer).WithMany(p => p.ShoppingCarts)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("ShoppingCart_CustomerId_fkey");
        });

        modelBuilder.Entity<ShoppingCartDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ShoppingCartDetails_pkey");

            entity.Property(e => e.SoLuongTrongGh).HasColumnName("SoLuongTrongGH");

            entity.HasOne(d => d.Cart).WithMany(p => p.ShoppingCartDetails)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("ShoppingCartDetails_CartId_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.ShoppingCartDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("ShoppingCartDetails_ProductId_fkey");
        });

        modelBuilder.Entity<TonKhoSummary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("TonKhoSummary_pkey");

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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
