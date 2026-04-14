using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class ProductReview
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public int? CustomerId { get; set; }

    public int? DiemDg { get; set; }

    public string? NoiDungDg { get; set; }

    public DateTime NgayDg { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Product? Product { get; set; }
}
