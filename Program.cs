using Microsoft.EntityFrameworkCore;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var webSocketConnections = new List<WebSocket>();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MessageDb>(opt => opt.UseInMemoryDatabase("messages"));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
var jsonSerializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
};

app.UseCors();
app.UseWebSockets(webSocketOptions);
app.MapGet("/", () => "Hello World!");
app.MapGet("/health-check", () => Results.Ok("Healty"));

app.Map("/ws", async (HttpContext context) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        webSocketConnections.Add(webSocket);

        await HandleWebSocketConnection(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

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

    await BroadcastMessageAsync(new { Action = "Deleted", Message = message });

    return Results.Ok(message);
});

app.MapPut("/messages/{id}", async (Guid id, MessageBody messageBody, MessageDb db) =>
{
    var message = await db.Messages.FindAsync(id);

    if (message == null)
    {
        return Results.NotFound(new { Message = "Message not found" });
    }

    message.Content = messageBody.Content;
    await db.SaveChangesAsync();

    await BroadcastMessageAsync(new { Action = "Edited", Message = message });

    return Results.Ok(message);
});

app.MapPost("/messages", async (MessageBody messageBody, MessageDb db) =>
{
    var message = new Message
    {
        Content = messageBody.Content,
        SenderName = messageBody.SenderName,
        Timestamp = messageBody.Timestamp
    };

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    await BroadcastMessageAsync(new { Action = "Created", Message = message });

    return Results.Ok(message);
});


async Task HandleWebSocketConnection(WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];
    while (webSocket.State == WebSocketState.Open)
    {
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            webSocketConnections.Remove(webSocket);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
        }
    }
}

async Task BroadcastMessageAsync(object message)
{
    var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
});
    var messageBytes = Encoding.UTF8.GetBytes(messageJson);

    foreach (var socket in webSocketConnections.ToList())
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        else
        {
            webSocketConnections.Remove(socket);
        }
    }
}

app.Run();
