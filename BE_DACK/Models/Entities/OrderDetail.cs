using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class OrderDetail
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public int? ProductId { get; set; }

    public int SoLuongSp { get; set; }

    public decimal Gia { get; set; }

    public string? TrangThai { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Product? Product { get; set; }
}
