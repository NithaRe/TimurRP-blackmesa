namespace Content.Server._BlackM.ChatImage;

public sealed class ChatImageException : Exception
{
    public string LocKey { get; }
    public (string, object)[] Args { get; }

    public ChatImageException(string locKey, params (string, object)[] args) : base(locKey)
    {
        LocKey = locKey;
        Args = args;
    }
}
