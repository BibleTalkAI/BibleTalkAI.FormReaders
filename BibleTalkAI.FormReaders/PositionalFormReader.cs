using BibleTalkAI.ObjectPools;

namespace BibleTalkAI.FormReaders;

public class PositionalFormReader
    (IStringBuilderPool stringBuilderPool, IDictionaryPool dictionaryPool)
    : FormReaderBase(stringBuilderPool, dictionaryPool)
{
    protected new bool UseReadCharCustomKey = true;

    protected new int ValueCountLimit = 10;
    protected new int KeyLengthLimit = 1;

    protected int Position = 0;

    public override void Reset()
    {
        base.Reset();
        Position = 0;
    }

    protected override void StartReadNextPair()
    {
        base.StartReadNextPair();
        Position = 0;
    }

    protected override bool ReadCharCustom(char c, int builderLength, char separator, out string? word)
    {
        if (builderLength == 0 && separator == '=')
        {
            Position++;

            word = Position switch
            {
                0 => PositionConstants.P_0,
                1 => PositionConstants.P_1,
                2 => PositionConstants.P_2,
                3 => PositionConstants.P_3,
                4 => PositionConstants.P_4,
                5 => PositionConstants.P_5,
                6 => PositionConstants.P_6,
                7 => PositionConstants.P_7,
                8 => PositionConstants.P_8,
                9 => PositionConstants.P_9,
                _ => Position.ToString()
            };

            return true;
        }

        word = null;
        return false;
    }
}
