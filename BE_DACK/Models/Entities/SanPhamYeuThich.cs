using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class SanPhamYeuThich
{
    public int Id { get; set; }

    public int? IdCustomer { get; set; }

    public int? IdProduct { get; set; }

    public virtual Customer? IdCustomerNavigation { get; set; }

    public virtual Product? IdProductNavigation { get; set; }
}
