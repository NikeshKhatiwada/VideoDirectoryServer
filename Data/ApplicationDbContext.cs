using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using VideoDirectory_Server.Models;

namespace VideoDirectory_Server.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Name=DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    public DbSet<User>? Users { get; set; }
    public DbSet<Channel>? Channels { get; set; }
    public DbSet<Video>? Videos { get; set; }
    public DbSet<Comment>? Comments { get; set; }
}
