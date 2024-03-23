﻿using System.Threading.Tasks;
using LinkDotNet.Blog.TestUtilities;
using LinkDotNet.Blog.Web.Features.Services;
using LinkDotNet.Blog.Web.Features.ShowBlogPost.Components;
using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.Blog.UnitTests.Web.Features.ShowBlogPost.Components;

public class LikeTests : TestContext
{
    [Fact]
    public void ShouldDisplayLikes()
    {
        Services.AddScoped(_ => Substitute.For<ILocalStorageService>());
        var blogPost = new BlogPostBuilder().WithLikes(1).Build();
        var cut = RenderComponent<Like>(
            p => p.Add(l => l.BlogPost, blogPost));

        var label = cut.Find("div").TextContent;

        label.Should().Contain("1");
    }

    [Fact]
    public void ShouldInvokeEventWhenButtonClicked()
    {
        Services.AddScoped(_ => Substitute.For<ILocalStorageService>());
        var blogPost = new BlogPostBuilder().Build();
        var wasClicked = false;
        var wasLike = false;
        var cut = RenderComponent<Like>(
            p => p.Add(l => l.BlogPost, blogPost)
                .Add(l => l.OnBlogPostLiked, b =>
                {
                    wasClicked = true;
                    wasLike = b;
                }));

        cut.Find("span").Click();

        wasClicked.Should().BeTrue();
        wasLike.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSetLocalStorageVariableOnClick()
    {
        var localStorage = Substitute.For<ILocalStorageService>();
        Services.AddScoped(_ => localStorage);
        var blogPost = new BlogPostBuilder().Build();
        blogPost.Id = "id";
        var cut = RenderComponent<Like>(
            p => p.Add(l => l.BlogPost, blogPost));

        cut.Find("span").Click();

        await localStorage.Received(1).SetItemAsync("hasLiked/id", true);
    }

    [Fact]
    public void ShouldCheckLocalStorageOnInit()
    {
        var localStorage = Substitute.For<ILocalStorageService>();
        localStorage.ContainKeyAsync("hasLiked/id").Returns(true);
        localStorage.GetItemAsync<bool>("hasLiked/id").Returns(true);
        Services.AddScoped(_ => localStorage);
        var blogPost = new BlogPostBuilder().Build();
        blogPost.Id = "id";
        var wasLike = true;
        var cut = RenderComponent<Like>(
            p => p.Add(l => l.BlogPost, blogPost)
                .Add(l => l.OnBlogPostLiked, b => wasLike = b));

        cut.Find("span").Click();

        wasLike.Should().BeFalse();
    }

    [Fact]
    public void ShouldCheckStorageOnClickAgainAndDoNothingOnMismatch()
    {
        var localStorage = Substitute.For<ILocalStorageService>();
        Services.AddScoped(_ => localStorage);
        var blogPost = new BlogPostBuilder().Build();
        blogPost.Id = "id";
        var wasClicked = false;
        var cut = RenderComponent<Like>(
            p => p.Add(l => l.BlogPost, blogPost)
                .Add(l => l.OnBlogPostLiked, _ => wasClicked = true));
        localStorage.ContainKeyAsync("hasLiked/id").Returns(true);
        localStorage.GetItemAsync<bool>("hasLiked/id").Returns(true);

        cut.Find("span").Click();

        wasClicked.Should().BeFalse();
    }
}