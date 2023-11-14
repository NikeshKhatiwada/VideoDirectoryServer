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
        modelBuilder.Entity<User>()
                .HasMany(u => u.SentMessages)
                .WithOne(m => m.Sender)
                .HasForeignKey(m => m.SenderId);

        modelBuilder.Entity<User>()
            .HasMany(u => u.ReceivedMessages)
            .WithOne(m => m.Receiver)
            .HasForeignKey(m => m.ReceiverId);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    public DbSet<SystemAdmin>? SystemAdmins { get; set; }
    public DbSet<User>? Users { get; set; }
    public DbSet<Channel>? Channels { get; set; }
    public DbSet<Video>? Videos { get; set; }
    public DbSet<Transcript>? Transcripts { get; set; }
    public DbSet<Comment>? Comments { get; set; }
    public DbSet<Tag>? Tags { get; set; }
    public DbSet<Message>? Messages { get; set; }
    public DbSet<VideoReport>? VideoReports { get; set; }
    public DbSet<CommentReport>? CommentReports { get; set; }
    public DbSet<UserReport>? UserReports { get; set; }
    public DbSet<ChannelReport>? ChannelReports { get; set; }
}
