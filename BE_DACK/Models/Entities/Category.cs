using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class Category
{
    public int Id { get; set; }

    public string TenDanhMucSp { get; set; } = null!;

    public string? MoTaDanhMuc { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
