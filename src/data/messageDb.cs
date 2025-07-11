using Microsoft.EntityFrameworkCore;

class MessageDb : DbContext
{
    public MessageDb(DbContextOptions<MessageDb> options) : base(options) { }

    public DbSet<Message> Messages => Set<Message>();
}