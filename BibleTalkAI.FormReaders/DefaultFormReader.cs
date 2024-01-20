using BibleTalkAI.ObjectPools;

namespace BibleTalkAI.FormReaders;

public class DefaultFormReader
    (IStringBuilderPool stringBuilderPool, IDictionaryPool dictionaryPool)
    : FormReaderBase(stringBuilderPool, dictionaryPool)
{
}