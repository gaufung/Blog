﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.TestUtilities;
using LinkDotNet.Blog.Web.Pages;
using LinkDotNet.Blog.Web.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace LinkDotNet.Blog.IntegrationTests.Web.Pages;

public class SearchByTagTests : SqlDatabaseTestBase<BlogPost>
{
    [Fact]
    public async Task ShouldOnlyDisplayTagsGivenByParameter()
    {
        using var ctx = new TestContext();
        await AddBlogPostWithTagAsync("Tag 1");
        await AddBlogPostWithTagAsync("Tag 1");
        await AddBlogPostWithTagAsync("Tag 2");
        ctx.Services.AddScoped<IRepository<BlogPost>>(_ => Repository);
        ctx.Services.AddScoped(_ => Mock.Of<IUserRecordService>());
        var cut = ctx.RenderComponent<SearchByTag>(p => p.Add(s => s.Tag, "Tag 1"));
        cut.WaitForState(() => cut.FindAll(".blog-card").Any());

        var tags = cut.FindAll(".blog-card");

        tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShouldHandleSpecialCharacters()
    {
        using var ctx = new TestContext();
        await AddBlogPostWithTagAsync("C#");
        ctx.Services.AddScoped<IRepository<BlogPost>>(_ => Repository);
        ctx.Services.AddScoped(_ => Mock.Of<IUserRecordService>());
        var cut = ctx.RenderComponent<SearchByTag>(p => p.Add(s => s.Tag, Uri.EscapeDataString("C#")));
        cut.WaitForState(() => cut.FindAll(".blog-card").Any());

        var tags = cut.FindAll(".blog-card");

        tags.Should().HaveCount(1);
    }

    private async Task AddBlogPostWithTagAsync(string tag)
    {
        var blogPost = new BlogPostBuilder().WithTags(tag).Build();
        await DbContext.AddAsync(blogPost);
        await DbContext.SaveChangesAsync();
    }
}