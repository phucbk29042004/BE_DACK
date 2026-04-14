using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class Order
{
    public int Id { get; set; }

    public int? CustomerId { get; set; }

    public DateTime NgayTaoDonHang { get; set; }

    public decimal TongGiaTriDonHang { get; set; }

    public string TrangThai { get; set; } = null!;

    public int? IdShipper { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Shipper? IdShipperNavigation { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
