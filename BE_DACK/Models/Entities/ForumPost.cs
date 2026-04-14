using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities
{
    public class ForumPost
    {
        public int Id { get; set; }
        public string TieuDe { get; set; }
        public string NoiDung { get; set; }
        public int CustomerId { get; set; }
        public int LuotXem { get; set; }
        public DateTime NgayTao { get; set; }

        // Navigation
        public virtual Customer Customer { get; set; }
        public virtual ICollection<ForumComment> ForumComments { get; set; }
    }
}
