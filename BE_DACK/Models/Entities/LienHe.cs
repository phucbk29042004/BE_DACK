using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class LienHe
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime? NgayGui { get; set; }
}
