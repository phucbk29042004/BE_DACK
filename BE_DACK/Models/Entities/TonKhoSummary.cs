using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class TonKhoSummary
{
    public int Id { get; set; }

    public string? TenHh { get; set; }

    public string? Dvt { get; set; }

    public int? TongSoLuongNhap { get; set; }

    public int? TongSoLuongXuat { get; set; }

    public int? TongSoTon { get; set; }
}
