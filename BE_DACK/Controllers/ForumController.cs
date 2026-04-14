using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_DACK.Models.Entities;

namespace BE_DACK.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ForumController : ControllerBase
    {
        private readonly DACKContext _context;

        public ForumController(DACKContext context)
        {
            _context = context;
        }

        // ============================================
        // BÀI VIẾT
        // ============================================

        // GET: api/Forum/posts
        // Lấy tất cả bài viết
        [HttpGet("posts")]
        public async Task<IActionResult> GetAllPosts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var skip = (page - 1) * pageSize;

                var totalPosts = await _context.ForumPosts.CountAsync();

                var posts = await _context.ForumPosts
                    .Include(p => p.Customer)
                    .Include(p => p.ForumComments)
                    .OrderByDescending(p => p.NgayTao)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        id = p.Id,
                        tieuDe = p.TieuDe,
                        noiDung = p.NoiDung.Length > 200 ? p.NoiDung.Substring(0, 200) + "..." : p.NoiDung,
                        tacGia = new
                        {
                            id = p.Customer.Id,
                            hoTen = p.Customer.HoTen,
                            email = p.Customer.Email
                        },
                        luotXem = p.LuotXem,
                        soBinhLuan = p.ForumComments.Count,
                        ngayTao = p.NgayTao
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách bài viết thành công",
                    data = posts,
                    pagination = new
                    {
                        currentPage = page,
                        pageSize = pageSize,
                        totalPages = (int)Math.Ceiling(totalPosts / (double)pageSize),
                        totalRecords = totalPosts
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách bài viết",
                    error = ex.Message
                });
            }
        }

        // GET: api/Forum/admin/posts
        // Lấy danh sách bài viết cho admin
        [HttpGet("admin/posts")]
        public async Task<IActionResult> GetAllPostsAdmin()
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var posts = await _context.ForumPosts
                    .Include(p => p.Customer)
                    .Include(p => p.ForumComments)
                    .OrderByDescending(p => p.NgayTao)
                    .Select(p => new
                    {
                        id = p.Id,
                        tieuDe = p.TieuDe,
                        tacGia = p.Customer != null ? new
                        {
                            id = p.Customer.Id,
                            hoTen = p.Customer.HoTen,
                            email = p.Customer.Email
                        } : null,
                        ngayTao = p.NgayTao,
                        luotXem = p.LuotXem,
                        soBinhLuan = p.ForumComments.Count
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách bài viết (admin) thành công",
                    data = posts,
                    total = posts.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách bài viết (admin)",
                    error = ex.Message
                });
            }
        }

        // GET: api/Forum/posts/5
        // Xem chi tiết bài viết
        [HttpGet("posts/{id}")]
        public async Task<IActionResult> GetPostById(int id)
        {
            try
            {
                var post = await _context.ForumPosts
                    .Include(p => p.Customer)
                    .Include(p => p.ForumComments)
                        .ThenInclude(c => c.Customer)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bài viết với ID {id}"
                    });
                }

                // Tăng lượt xem
                post.LuotXem++;
                await _context.SaveChangesAsync();

                var result = new
                {
                    id = post.Id,
                    tieuDe = post.TieuDe,
                    noiDung = post.NoiDung,
                    tacGia = new
                    {
                        id = post.Customer.Id,
                        hoTen = post.Customer.HoTen,
                        email = post.Customer.Email
                    },
                    luotXem = post.LuotXem,
                    ngayTao = post.NgayTao,
                    binhLuan = post.ForumComments
                        .OrderBy(c => c.NgayTao)
                        .Select(c => new
                        {
                            id = c.Id,
                            noiDung = c.NoiDung,
                            nguoiBinhLuan = new
                            {
                                id = c.Customer.Id,
                                hoTen = c.Customer.HoTen
                            },
                            ngayTao = c.NgayTao
                        }).ToList()
                };

                return Ok(new
                {
                    success = true,
                    message = "Lấy chi tiết bài viết thành công",
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy chi tiết bài viết",
                    error = ex.Message
                });
            }
        }

        // POST: api/Forum/posts
        // Tạo bài viết mới
        [HttpPost("posts")]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
        {
            try
            {
                // Lấy userId từ token
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập để tạo bài viết"
                    });
                }

                // Validate
                if (string.IsNullOrWhiteSpace(request.TieuDe))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tiêu đề không được để trống"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.NoiDung))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Nội dung không được để trống"
                    });
                }

                var post = new ForumPost
                {
                    TieuDe = request.TieuDe.Trim(),
                    NoiDung = request.NoiDung.Trim(),
                    CustomerId = userId,
                    LuotXem = 0,
                    NgayTao = DateTime.UtcNow
                };

                _context.ForumPosts.Add(post);
                await _context.SaveChangesAsync();

                // Load lại để lấy thông tin customer
                var createdPost = await _context.ForumPosts
                    .Include(p => p.Customer)
                    .FirstAsync(p => p.Id == post.Id);

                return CreatedAtAction(nameof(GetPostById), new { id = post.Id }, new
                {
                    success = true,
                    message = "Tạo bài viết thành công",
                    data = new
                    {
                        id = createdPost.Id,
                        tieuDe = createdPost.TieuDe,
                        noiDung = createdPost.NoiDung,
                        tacGia = new
                        {
                            id = createdPost.Customer.Id,
                            hoTen = createdPost.Customer.HoTen
                        },
                        ngayTao = createdPost.NgayTao
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tạo bài viết",
                    error = ex.Message
                });
            }
        }

        // PUT: api/Forum/posts/5
        // Sửa bài viết
        [HttpPut("posts/{id}")]
        public async Task<IActionResult> UpdatePost(int id, [FromBody] CreatePostRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập"
                    });
                }

                var post = await _context.ForumPosts.FindAsync(id);
                if (post == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bài viết với ID {id}"
                    });
                }

                // Kiểm tra quyền sở hữu
                if (post.CustomerId != userId)
                {
                    return Forbid();
                }

                // Validate
                if (string.IsNullOrWhiteSpace(request.TieuDe))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tiêu đề không được để trống"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.NoiDung))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Nội dung không được để trống"
                    });
                }

                post.TieuDe = request.TieuDe.Trim();
                post.NoiDung = request.NoiDung.Trim();
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật bài viết thành công",
                    data = new
                    {
                        id = post.Id,
                        tieuDe = post.TieuDe,
                        noiDung = post.NoiDung
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi cập nhật bài viết",
                    error = ex.Message
                });
            }
        }

        // PUT: api/Forum/admin/posts/5
        // Admin sửa bài viết (bỏ qua kiểm tra chủ sở hữu)
        [HttpPut("admin/posts/{id}")]
        public async Task<IActionResult> AdminUpdatePost(int id, [FromBody] CreatePostRequest request)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var post = await _context.ForumPosts.FindAsync(id);
                if (post == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bài viết với ID {id}"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.TieuDe) || string.IsNullOrWhiteSpace(request.NoiDung))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Tiêu đề và nội dung không được để trống"
                    });
                }

                post.TieuDe = request.TieuDe.Trim();
                post.NoiDung = request.NoiDung.Trim();
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Admin đã cập nhật bài viết thành công",
                    data = new
                    {
                        id = post.Id,
                        tieuDe = post.TieuDe,
                        noiDung = post.NoiDung
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi admin cập nhật bài viết",
                    error = ex.Message
                });
            }
        }

        // DELETE: api/Forum/posts/5
        // Xóa bài viết
        [HttpDelete("posts/{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập"
                    });
                }

                var post = await _context.ForumPosts.FindAsync(id);
                if (post == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bài viết với ID {id}"
                    });
                }

                // Kiểm tra quyền sở hữu
                if (post.CustomerId != userId)
                {
                    return Forbid();
                }

                _context.ForumPosts.Remove(post);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Xóa bài viết thành công",
                    data = new { id = id }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi xóa bài viết",
                    error = ex.Message
                });
            }
        }

        // DELETE: api/Forum/admin/posts/5
        // Admin xóa bài viết (và các bình luận liên quan)
        [HttpDelete("admin/posts/{id}")]
        public async Task<IActionResult> AdminDeletePost(int id)
        {
            try
            {
                var isAdminClaim = User.Claims.FirstOrDefault(c => c.Type == "isAdmin");
                if (isAdminClaim == null || isAdminClaim.Value != "True")
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập chức năng này" });
                }

                var post = await _context.ForumPosts
                    .Include(p => p.ForumComments)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bài viết với ID {id}"
                    });
                }

                if (post.ForumComments.Any())
                {
                    _context.ForumComments.RemoveRange(post.ForumComments);
                }

                _context.ForumPosts.Remove(post);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Admin đã xóa bài viết thành công",
                    data = new { id = id }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi admin xóa bài viết",
                    error = ex.Message
                });
            }
        }

        // GET: api/Forum/my-posts
        [HttpGet("my-posts")]
        public async Task<IActionResult> GetMyPosts()
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập"
                    });
                }

                var posts = await _context.ForumPosts
                    .Include(p => p.ForumComments)
                    .Where(p => p.CustomerId == userId)
                    .OrderByDescending(p => p.NgayTao)
                    .Select(p => new
                    {
                        id = p.Id,
                        tieuDe = p.TieuDe,
                        noiDung = p.NoiDung.Length > 200 ? p.NoiDung.Substring(0, 200) + "..." : p.NoiDung,
                        luotXem = p.LuotXem,
                        soBinhLuan = p.ForumComments.Count,
                        ngayTao = p.NgayTao
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách bài viết của bạn thành công",
                    data = posts,
                    total = posts.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách bài viết",
                    error = ex.Message
                });
            }
        }

        // ============================================
        // BÌNH LUẬN
        // ============================================

        // GET: api/Forum/posts/5/comments
        // Lấy tất cả comment của 1 bài viết
        [HttpGet("posts/{postId}/comments")]
        public async Task<IActionResult> GetCommentsByPostId(int postId)
        {
            try
            {
                var post = await _context.ForumPosts.FindAsync(postId);
                if (post == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bài viết với ID {postId}"
                    });
                }

                var comments = await _context.ForumComments
                    .Include(c => c.Customer)
                    .Where(c => c.PostId == postId)
                    .OrderBy(c => c.NgayTao)
                    .Select(c => new
                    {
                        id = c.Id,
                        noiDung = c.NoiDung,
                        nguoiBinhLuan = new
                        {
                            id = c.Customer.Id,
                            hoTen = c.Customer.HoTen
                        },
                        ngayTao = c.NgayTao
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách bình luận thành công",
                    data = comments,
                    total = comments.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách bình luận",
                    error = ex.Message
                });
            }
        }

        // POST: api/Forum/comments
        // Tạo bình luận mới
        [HttpPost("comments")]
        public async Task<IActionResult> CreateComment([FromBody] CreateCommentRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập để bình luận"
                    });
                }

                // Kiểm tra bài viết có tồn tại
                var post = await _context.ForumPosts.FindAsync(request.PostId);
                if (post == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bài viết với ID {request.PostId}"
                    });
                }

                // Validate
                if (string.IsNullOrWhiteSpace(request.NoiDung))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Nội dung bình luận không được để trống"
                    });
                }

                var comment = new ForumComment
                {
                    PostId = request.PostId,
                    CustomerId = userId,
                    NoiDung = request.NoiDung.Trim(),
                    NgayTao = DateTime.UtcNow
                };

                _context.ForumComments.Add(comment);
                await _context.SaveChangesAsync();

                // Load lại để lấy thông tin customer
                var createdComment = await _context.ForumComments
                    .Include(c => c.Customer)
                    .FirstAsync(c => c.Id == comment.Id);

                return CreatedAtAction(nameof(GetCommentsByPostId), new { postId = request.PostId }, new
                {
                    success = true,
                    message = "Tạo bình luận thành công",
                    data = new
                    {
                        id = createdComment.Id,
                        noiDung = createdComment.NoiDung,
                        nguoiBinhLuan = new
                        {
                            id = createdComment.Customer.Id,
                            hoTen = createdComment.Customer.HoTen
                        },
                        ngayTao = createdComment.NgayTao
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi tạo bình luận",
                    error = ex.Message
                });
            }
        }

        // PUT: api/Forum/comments/5
        // Sửa bình luận
        [HttpPut("comments/{id}")]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] UpdateCommentRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập"
                    });
                }

                var comment = await _context.ForumComments.FindAsync(id);
                if (comment == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bình luận với ID {id}"
                    });
                }

                // Kiểm tra quyền sở hữu
                if (comment.CustomerId != userId)
                {
                    return Forbid();
                }

                // Validate
                if (string.IsNullOrWhiteSpace(request.NoiDung))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Nội dung bình luận không được để trống"
                    });
                }

                comment.NoiDung = request.NoiDung.Trim();
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật bình luận thành công",
                    data = new
                    {
                        id = comment.Id,
                        noiDung = comment.NoiDung
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi cập nhật bình luận",
                    error = ex.Message
                });
            }
        }

        // DELETE: api/Forum/comments/5
        // Xóa bình luận
        [HttpDelete("comments/{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("id")?.Value ?? "0");
                if (userId <= 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Bạn cần đăng nhập"
                    });
                }

                var comment = await _context.ForumComments.FindAsync(id);
                if (comment == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Không tìm thấy bình luận với ID {id}"
                    });
                }

                // Kiểm tra quyền sở hữu
                if (comment.CustomerId != userId)
                {
                    return Forbid();
                }

                _context.ForumComments.Remove(comment);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Xóa bình luận thành công",
                    data = new { id = id }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi xóa bình luận",
                    error = ex.Message
                });
            }
        }
    }
    public class CreatePostRequest
    {
        public string TieuDe { get; set; } = null!;
        public string NoiDung { get; set; } = null!;
    }

    public class CreateCommentRequest
    {
        public int PostId { get; set; }
        public string NoiDung { get; set; } = null!;
    }

    public class UpdateCommentRequest
    {
        public string NoiDung { get; set; } = null!;
    }
}
