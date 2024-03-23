using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System;
using System.Threading.Tasks;

namespace LinkDotNet.Blog.Web.Controller;

[ApiController]
[Route("api/blogpost")]
[Authorize]
public sealed class BlogPostController : ControllerBase
{
    private readonly IRepository<BlogPost> blogPostRepository;

    public BlogPostController(
        IRepository<BlogPost> blogPostRepository)
    {
        ArgumentNullException.ThrowIfNull(blogPostRepository);
        this.blogPostRepository = blogPostRepository;
    }

    [HttpGet]
    [Route("all")]
    public async Task<IActionResult> GetBlogPost()
    {
        var blogPosts = await blogPostRepository.GetAllAsync();
        return Ok(blogPosts);
    }

    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateBlogPost([FromBody] BlogPostModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var blogPost = BlogPost.Create(model.Title, model.ShortDescription, model.Content, model.PreviewImageUrl, true, null, null, model.Tags);
        await blogPostRepository.StoreAsync(blogPost);
        return Ok();
    }
}
