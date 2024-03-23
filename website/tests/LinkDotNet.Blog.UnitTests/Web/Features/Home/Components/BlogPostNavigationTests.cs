﻿using System.Linq;
using AngleSharp.Html.Dom;
using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure;
using LinkDotNet.Blog.Web.Features.Home.Components;

namespace LinkDotNet.Blog.UnitTests.Web.Features.Home.Components;

public class BlogPostNavigationTests : TestContext
{
    [Fact]
    public void ShouldFireEventWhenGoingToNextPage()
    {
        var page = CreatePagedList(2, 3);

        var cut = RenderComponent<BlogPostNavigation<BlogPost>>(p =>
            p.Add(param => param.PageList, page));

        cut.FindAll("a").Cast<IHtmlAnchorElement>().Last().Href.Should().EndWith("/3");
    }

    [Fact]
    public void ShouldFireEventWhenGoingToPreviousPage()
    {
        var page = CreatePagedList(2, 3);

        var cut = RenderComponent<BlogPostNavigation<BlogPost>>(p =>
            p.Add(param => param.PageList, page));

        cut.FindAll("a").Cast<IHtmlAnchorElement>().First().Href.Should().EndWith("/1");
    }

    [Fact]
    public void ShouldNotFireNextWhenOnLastPage()
    {
        var page = CreatePagedList(2, 2);
        var cut = RenderComponent<BlogPostNavigation<BlogPost>>(p =>
            p.Add(param => param.PageList, page));

        cut.Find("li:last-child").ClassList.Should().Contain("disabled");
    }

    [Fact]
    public void ShouldNotFireNextWhenOnFirstPage()
    {
        var page = CreatePagedList(1, 2);
        var cut = RenderComponent<BlogPostNavigation<BlogPost>>(p =>
            p.Add(param => param.PageList, page));

        cut.Find("li:first-child").ClassList.Should().Contain("disabled");
    }

    [Fact]
    public void ShouldNotFireNextWhenNoPage()
    {
        var page = CreatePagedList(0, 0);
        var cut = RenderComponent<BlogPostNavigation<BlogPost>>(p =>
            p.Add(param => param.PageList, page));

        cut.Find("li:first-child").ClassList.Should().Contain("disabled");
        cut.Find("li:last-child").ClassList.Should().Contain("disabled");
    }

    private static IPagedList<BlogPost> CreatePagedList(int currentPage, int pageCount)
    {
        var page = Substitute.For<IPagedList<BlogPost>>();
        page.PageNumber.Returns(currentPage);
        page.IsFirstPage.Returns(currentPage == 1);
        page.IsLastPage.Returns(currentPage == pageCount);

        return page;
    }
}
