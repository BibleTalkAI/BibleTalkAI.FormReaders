using BibleTalkAI.ObjectPools;
using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.Text;

namespace BibleTalkAI.FormReaders;

/// <summary>
/// Custom reimplementation Microsoft.AspNetCore.WebUtilities.FormReader
/// Uses object pools for StringBuilder and Dictionary
/// Allows for custom ReadChar implementations
/// Uses lower memory footprint
/// Resettable for reuse in object pool
/// </summary>
public abstract class FormReaderBase
    (IStringBuilderPool stringBuilderPool, IDictionaryPool dictionaryPool)
    : IFormReader
{
    protected static readonly ArrayPool<char> _charPool = ArrayPool<char>.Shared;

    protected int ValueCountLimit = 100;
    protected int KeyLengthLimit = 512;
    protected int ValueLengthLimit = 1024 * 1024 * 4;
    protected int _rentedCharPoolLength = 2048;

    protected char[]? _buffer;
    protected TextReader? _reader;
    protected StringBuilder? _builder;
    protected int _bufferOffset;
    protected int _bufferCount;
    protected string? _currentKey;
    protected string? _currentValue;
    protected bool _endOfStream;
    protected bool _skipCurrent;
    protected bool _error;

    protected int _position = 0;

    protected bool UseReadCharCustomKey = false;
    protected bool UseReadCharCustomValue = false;

    protected HashSet<string>? _keys;

    /// <summary>
    /// Initializes a new instance of <see cref="FormReader"/>.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to read.</param>
    /// <param name="stringBuilderPool">The <see cref="ObjectPool{T}"/> to use.</param>
    /// <param name="dictionaryPool">The <see cref="ObjectPool{T}"/> to use.</param>
    public virtual Dictionary<string, string?>? ReadForm(Stream stream, HashSet<string> keys)
    {
        if (stream == null)
        {
            return null;
        }

        _keys = keys;
        _buffer = _charPool.Rent(_rentedCharPoolLength);
        _reader = new StreamReader(stream, Encoding.UTF8, bufferSize: 1024 * 2, leaveOpen: true);
        _builder ??= stringBuilderPool.Get();

        return ReadForm();
    }

    public virtual string?[]? ReadForm(Stream stream, int capacity)
    {
        if (stream == null)
        {
            return null;
        }

        _buffer = _charPool.Rent(_rentedCharPoolLength);
        _reader = new StreamReader(stream, Encoding.UTF8, bufferSize: 1024 * 2, leaveOpen: true);
        _builder ??= stringBuilderPool.Get();

        return ReadForm(capacity);
    }

    public virtual void Reset()
    {
        if (_buffer != null)
        {
            _charPool.Return(_buffer);
            _buffer = null;
        }
        if (_builder != null)
        {
            stringBuilderPool.Return(_builder);
            _builder = null;
        }
        _position = 0;
        _bufferOffset = 0;
        _bufferCount = 0;
        _currentKey = null;
        _currentValue = null;
        _endOfStream = false;
        _skipCurrent = false;
        _error = false;
    }

    protected virtual void StartReadNextPair()
    {
        _currentKey = null;
        _currentValue = null;
        _skipCurrent = false;
        _error = false;
    }

    protected virtual bool ReadCharCustom(char c, int builderLength, char separator, out string? word)
    {
        word = null;
        return false;
    }

    protected virtual bool ReadChar(char separator, int limit, bool skip, out string? word)
    {
        if (skip)
        {
            word = null;

            char _c = _buffer![_bufferOffset++];
            _bufferCount--;

            return _c == separator;
        }

        _builder ??= stringBuilderPool.Get();

        // End
        if (_bufferCount == 0)
        {
            word = _skipCurrent ? null : BuildWord();
            if (_skipCurrent)
            {
                _builder.Clear();
            }
            return true;
        }

        var c = _buffer![_bufferOffset++];
        _bufferCount--;

        int builderLength = _builder.Length;
        if (((UseReadCharCustomKey && separator == '=') || (UseReadCharCustomValue && separator == '&'))
            && ReadCharCustom(c, builderLength, separator, out string? w))
        {
            word = w;
            return true;
        }

        if (c == separator)
        {
            word = _skipCurrent ? null : BuildWord();
            if (_skipCurrent)
            {
                _builder.Clear();
            }
            return true;
        }
        if (builderLength >= limit)
        {
            _error = true;
            word = null;
            return true;
            //throw new InvalidDataException("Form key or value length limit exceeded.");
        }
        _builder.Append(c);
        word = null;
        return false;
    }

    // '+' un-escapes to ' ', %HH un-escapes as ASCII (or utf-8?)
    protected virtual string BuildWord()
    {
        _builder!.Replace('+', ' ');
        var result = _builder.ToString();
        _builder.Clear();
        return Uri.UnescapeDataString(result);
    }

    protected void Buffer()
    {
        _bufferOffset = 0;
        _bufferCount = _reader!.Read(_buffer!, 0, _buffer!.Length);
        _endOfStream = _bufferCount == 0;
    }

    protected void ReadNextPairImpl(bool skipKey = false)
    {
        StartReadNextPair();
        while (!_endOfStream)
        {
            // Empty
            if (_bufferCount == 0)
            {
                Buffer();
            }
            if (TryReadNextPair(skipKey))
            {
                break;
            }
            if (_error)
            {
                break;
            }
        }
    }

    protected bool TryReadNextPair(bool skipKey)
    {
        if (_currentKey == null)
        {
            if (!TryReadWord('=', KeyLengthLimit, skipKey, out _currentKey))
            {
                return false;
            }

            if (_bufferCount == 0)
            {
                return false;
            }
        }

        if (_error)
        {
            return true;
        }

        if (_currentValue == null)
        {
            if (!_skipCurrent && !string.IsNullOrEmpty(_currentKey))
            {
                // check if hashset contains _currentKey
                if (_keys != null && !_keys.Contains(_currentKey))
                {
                    _skipCurrent = true;
                }
            }

            if (!TryReadWord('&', ValueLengthLimit, false, out _currentValue))
            {
                return false;
            }
        }
        return true;
    }

    protected bool TryReadWord(char separator, int limit, bool skip, out string? value)
    {
        do
        {
            if (ReadChar(separator, limit, skip, out value))
            {
                return true;
            }

            if (_error)
            {
                return true;
            }
        } while (_bufferCount > 0);
        return false;
    }

    /// <summary>
    /// Parses text from an HTTP form body.
    /// </summary>
    /// <returns>The collection containing the parsed HTTP form body.</returns>
    protected virtual Dictionary<string, string?>? ReadForm()
    {
        var formValues = dictionaryPool.Get();
        while (!_endOfStream)
        {
            ReadNextPairImpl();

            if (_error) return null;

            if (_currentKey != null && _currentValue != null)
            {
                formValues[_currentKey] = _currentValue;
                if (formValues.Count > ValueCountLimit)
                {
                    // error, invalid request
                    return null;
                }
            }
        }
        return formValues;
    }

    /// <summary>
    /// Parses text from an HTTP form body.
    /// </summary>
    /// <returns>The collection containing the parsed HTTP form body.</returns>
    protected virtual string?[]? ReadForm(int capacity)
    {
        var formValues = ArrayPool<string?>.Shared.Rent(capacity);
        while (!_endOfStream)
        {
            ReadNextPairImpl(skipKey: true);

            if (_error) return null;

            formValues[_position] = _currentValue;
            if (_position >= capacity)
            {
                // skip, invalid request
                break;
            }
        }
        return formValues;
    }
}
