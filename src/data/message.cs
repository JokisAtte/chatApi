
public class Message : MessageBody
{
    public Guid Id {get; set;}
}

public class MessageBody
{
    public string Content { get; set; }
    public string SenderName {get; set; }
}
