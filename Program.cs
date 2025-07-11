using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MessageDb>(opt => opt.UseInMemoryDatabase("messages"));
var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapGet("/health-check", () => Results.Ok("Healty"));

app.MapGet("/messages", async (MessageDb db) =>
{
    var messages = await db.Messages.ToListAsync();
    return Results.Ok(messages);
});

app.MapDelete("/messages/{id}", async (Guid id, MessageDb db) =>
{
    var message = await db.Messages.FindAsync(id);

    if (message == null)
    {
        return Results.NotFound(new { Message = "Message not found" });
    }
    
    db.Messages.Remove(message);
    await db.SaveChangesAsync();

    return Results.Ok(message);
});

app.MapPost("/message", async (MessageBody messageBody, MessageDb db) =>
{
    var message = new Message
    {
        Content = messageBody.Content,
        SenderName = messageBody.SenderName
    };

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    return Results.Ok(message);
});

app.Run();
