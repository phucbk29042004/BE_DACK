using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class AccountType
{
    public int Id { get; set; }

    public string TenLoaiTaiKhoan { get; set; } = null!;

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
