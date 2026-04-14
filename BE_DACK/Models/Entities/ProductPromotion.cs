using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class ProductPromotion
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public int? PromotionId { get; set; }

    public virtual Product? Product { get; set; }

    public virtual Promotion? Promotion { get; set; }
}
