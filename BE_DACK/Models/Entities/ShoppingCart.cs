using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class ShoppingCart
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<ShoppingCartDetail> ShoppingCartDetails { get; set; } = new List<ShoppingCartDetail>();
}
