// EF Core (InMemory sağlayıcı) ile ProjectTo ve takip edilen varlığa eşleme senaryoları.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VeloxMapper;
using VeloxMapper.QueryableExtensions;

namespace VeloxMapper.Tests.Migration.EfCore;

public class Blog
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public Author? Author { get; set; }
    public List<Post> Posts { get; set; } = new();
}

public class Author
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public class Post
{
    public int Id { get; set; }
    public string Headline { get; set; } = "";
    public int Likes { get; set; }
}

public class BlogDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? AuthorFullName { get; set; }
    public string? AuthorFirstName { get; set; }
    public int PostCount { get; set; }
    public List<PostDto> Posts { get; set; } = new();
}

public class PostDto
{
    public string Headline { get; set; } = "";
    public int Likes { get; set; }
}

public class BlogUpdateDto
{
    public string Title { get; set; } = "";
    public List<PostDto> Posts { get; set; } = new();
}

public class BlogContext : DbContext
{
    public BlogContext(DbContextOptions<BlogContext> options) : base(options) { }
    public DbSet<Blog> Blogs => Set<Blog>();
}

public class EfCoreIntegrationTests
{
    private static readonly MapperConfiguration Config = new(cfg =>
    {
        cfg.CreateMap<Post, PostDto>().ReverseMap();
        cfg.CreateMap<Blog, BlogDto>()
            .ForMember(d => d.AuthorFullName, o => o.MapFrom(s => s.Author!.FirstName + " " + s.Author.LastName))
            .ForMember(d => d.PostCount, o => o.MapFrom(s => s.Posts.Count));
        cfg.CreateMap<BlogUpdateDto, Blog>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Author, o => o.Ignore());
    });

    private static BlogContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BlogContext>().UseInMemoryDatabase("blogs-" + Guid.NewGuid()).Options;
        var context = new BlogContext(options);
        context.Blogs.Add(new Blog
        {
            Title = "VeloxMapper",
            Author = new Author { FirstName = "Umut", LastName = "Özer" },
            Posts = { new Post { Headline = "Merhaba", Likes = 3 }, new Post { Headline = "Geçiş", Likes = 7 } }
        });
        context.Blogs.Add(new Blog { Title = "Yazarsız" });
        context.SaveChanges();
        context.ChangeTracker.Clear();
        return context;
    }

    [Fact]
    public void ProjectTo_EF_Core_sorgusuna_cevrilir()
    {
        Config.AssertConfigurationIsValid();
        using var context = CreateContext();

        var blogs = context.Blogs.OrderBy(b => b.Title).ProjectTo<BlogDto>(Config).ToList();

        Assert.Equal(2, blogs.Count);
        Assert.Equal("Umut Özer", blogs[0].AuthorFullName);
        Assert.Equal("Umut", blogs[0].AuthorFirstName);        // flattening
        Assert.Equal(2, blogs[0].PostCount);
        Assert.Equal(new[] { "Merhaba", "Geçiş" }, blogs[0].Posts.Select(p => p.Headline).OrderByDescending(h => h == "Merhaba"));
        Assert.Null(blogs[1].AuthorFirstName);
        Assert.Empty(blogs[1].Posts);
    }

    [Fact]
    public void Takip_edilen_varliga_esleme_koleksiyon_ornegini_korur()
    {
        using var context = CreateContext();
        var blog = context.Blogs.Include(b => b.Posts).Include(b => b.Author).Single(b => b.Title == "VeloxMapper");
        var posts = blog.Posts;
        var mapper = Config.CreateMapper();

        mapper.Map(new BlogUpdateDto { Title = "Yeni başlık", Posts = { new PostDto { Headline = "Tek", Likes = 1 } } }, blog);
        context.SaveChanges();

        Assert.Same(posts, blog.Posts);
        Assert.Equal("Yeni başlık", context.Blogs.Single(b => b.Id == blog.Id).Title);
        Assert.Single(blog.Posts);
        Assert.NotNull(blog.Author); // ignore edilen navigasyon korunur
    }
}
