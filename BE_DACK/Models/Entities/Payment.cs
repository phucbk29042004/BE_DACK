using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class Payment
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public DateTime NgayThanhToan { get; set; }

    public decimal SoTienThanhToan { get; set; }

    public string PhuongThucThanhToan { get; set; } = null!;

    public string TrangThai { get; set; } = null!;

    public virtual Order? Order { get; set; }
}
