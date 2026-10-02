using System.Text;

namespace Analytics.Infrastructure.Reports.Pdf;

// Die Standardschriften kennen nur WinAnsi, im Wesentlichen Latin-1. Umlaute passen, ein paar
// typografische Zeichen liegen woanders, und was es dort nicht gibt, wird ersetzt statt verloren.
public static class WinAnsi
{
    private static readonly Dictionary<char, char> Special = new Dictionary<char, char>
    {
        ['€'] = (char)0x80,
        ['…'] = (char)0x85,
        ['‘'] = (char)0x91,
        ['’'] = (char)0x92,
        ['“'] = (char)0x93,
        ['”'] = (char)0x94,
        ['•'] = (char)0x95,
        ['–'] = (char)0x96,
        ['—'] = (char)0x97,
        ['−'] = '-'
    };

    // Liefert die Zeichen als Bytewerte 0 bis 255 und maskiert, was in einem PDF-String eine
    // Bedeutung hat.
    public static string Escape(string text)
    {
        StringBuilder escaped = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            char mapped = Map(character);
            if (mapped is '\\' or '(' or ')')
            {
                escaped.Append('\\');
            }

            escaped.Append(mapped);
        }

        return escaped.ToString();
    }

    private static char Map(char character)
    {
        if (Special.TryGetValue(character, out char special))
        {
            return special;
        }

        if ((character >= 0x20 && character <= 0x7E) || (character >= 0xA0 && character <= 0xFF))
        {
            return character;
        }

        return '?';
    }
}
