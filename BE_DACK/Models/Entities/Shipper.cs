using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class Shipper
{
    public int ShipperId { get; set; }

    public string TenShipper { get; set; } = null!;

    public string DienThoai { get; set; } = null!;

    public string? Email { get; set; }

    public bool? TrangThai { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
