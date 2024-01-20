using Microsoft.Extensions.ObjectPool;

namespace BibleTalkAI.FormReaders;

public class DefaultFormReaderPool
    (ObjectPool<DefaultFormReader> pool)
    : IDefaultFormReaderPool
{
    public DefaultFormReader Get() => pool.Get();

    public void Return(DefaultFormReader instance) => pool.Return(instance);
}
