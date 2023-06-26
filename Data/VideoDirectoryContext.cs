using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace VideoDirectory_Server.Data;

public partial class VideoDirectoryContext : DbContext
{
    public VideoDirectoryContext()
    {
    }

    public VideoDirectoryContext(DbContextOptions<VideoDirectoryContext> options)
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
}
