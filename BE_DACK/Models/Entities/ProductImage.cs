using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string? HinhAnh { get; set; }

    public virtual Product Product { get; set; } = null!;
}
