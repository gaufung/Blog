﻿using System.Threading.Tasks;
using Blazored.Toast.Services;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.Web.Pages.Admin;
using LinkDotNet.Blog.Web.Shared;
using LinkDotNet.Blog.Web.Shared.Admin;
using LinkDotNet.Blog.Web.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace LinkDotNet.Blog.IntegrationTests.Web.Pages.Admin;

public class CreateNewBlogPostPageTests : SqlDatabaseTestBase<BlogPost>
{
    [Fact]
    public async Task ShouldSaveBlogPostOnSave()
    {
        using var ctx = new TestContext();
        var toastService = new Mock<IToastService>();
        ctx.AddTestAuthorization().SetAuthorized("some username");
        ctx.Services.AddScoped<IRepository<BlogPost>>(_ => Repository);
        ctx.Services.AddScoped(_ => toastService.Object);
        ctx.ComponentFactories.AddStub<UploadFile>();
        ctx.Services.AddScoped(_ => Mock.Of<IFileProcessor>());
        using var cut = ctx.RenderComponent<CreateNewBlogPostPage>();
        var newBlogPost = cut.FindComponent<CreateNewBlogPost>();

        TriggerNewBlogPost(newBlogPost);

        var blogPostFromDb = await DbContext.BlogPosts.SingleOrDefaultAsync(t => t.Title == "My Title");
        blogPostFromDb.Should().NotBeNull();
        blogPostFromDb.ShortDescription.Should().Be("My short Description");
        toastService.Verify(t => t.ShowInfo("Created BlogPost My Title", string.Empty, null), Times.Once);
    }

    private static void TriggerNewBlogPost(IRenderedComponent<CreateNewBlogPost> cut)
    {
        cut.Find("#title").Input("My Title");
        cut.Find("#short").Input("My short Description");
        cut.Find("#content").Input("My content");
        cut.Find("#preview").Change("My preview url");
        cut.Find("#published").Change(false);
        cut.Find("#tags").Change("Tag1,Tag2,Tag3");

        cut.Find("form").Submit();
    }
}