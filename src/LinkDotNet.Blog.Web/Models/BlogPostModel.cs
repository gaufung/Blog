using System.Collections.Generic;

namespace LinkDotNet.Blog.Web.Models;

public sealed class BlogPostModel
{
    public string Title { get; set; }

    public string ShortDescription { get; set; }

    public string Content { get; set; }

    public string PreviewImageUrl { get; set; }

    public string PreviewImageUrlFallback { get;  set; }

    public IReadOnlyList<string> Tags { get; set; }
}
