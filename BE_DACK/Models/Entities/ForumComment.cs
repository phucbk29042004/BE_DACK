using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities
{
    public class ForumComment
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int CustomerId { get; set; }
        public string NoiDung { get; set; }
        public DateTime NgayTao { get; set; }

        // Navigation
        public virtual ForumPost Post { get; set; }
        public virtual Customer Customer { get; set; }
    }
}
