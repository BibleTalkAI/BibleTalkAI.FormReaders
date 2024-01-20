namespace BibleTalkAI.FormReaders;

public interface IFormReader
{
    Dictionary<string, string?>? ReadForm(Stream stream, HashSet<string> keys);
    string?[]? ReadForm(Stream stream, int capacity);
    void Reset();
}
