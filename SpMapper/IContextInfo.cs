namespace Kama.DatabaseModel
{
    public interface IContextInfo
    {
        string Key { get; }

        string Value();
    }
}
