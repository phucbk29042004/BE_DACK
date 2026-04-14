using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class ShoppingCartDetail
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public int? SoLuongTrongGh { get; set; }

    public int? CartId { get; set; }

    public virtual ShoppingCart? Cart { get; set; }

    public virtual Product? Product { get; set; }
}
