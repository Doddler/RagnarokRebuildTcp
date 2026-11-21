using RebuildSharedData.Config;

namespace RebuildSharedData.Util;

//Shared so the client can explain a bad name without a round trip; the server enforces it.
public static class NameValidator
{
    //null when the name is valid
    public static string? GetCharacterNameError(string name) =>
        GetError(name, "character name", SharedConfig.MinPlayerName, SharedConfig.MaxPlayerName);
    public static string? GetPartyNameError(string name) =>
        GetError(name, "party name", SharedConfig.MinPartyName, SharedConfig.MaxPartyName);

    public static bool IsAllowedChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or ' ';

    //for typing: an allowed character, but no leading space and no space next to another
    public static bool IsAllowedAt(string text, int index, char c)
    {
        if (!IsAllowedChar(c))
            return false;
        if (c != ' ')
            return true;
        return index > 0 && text[index - 1] != ' ' && (index >= text.Length || text[index] != ' ');
    }

    //for an input field's character filter: '\0' rejects the typed character
    public static char FilterTyped(string text, int index, char c) => IsAllowedAt(text, index, c) ? c : '\0';

    private static string? GetError(string name, string kind, int minLength, int maxLength)
    {
        if (name.Length > maxLength)
            return $"The {kind} you entered is too long, it must be {maxLength} or fewer characters in length.";

        if (string.IsNullOrWhiteSpace(name) || name.Length < minLength)
            return $"The {kind} is invalid. It must be at least {minLength} characters long.";

        if (name.Trim() != name)
            return $"The {kind} is invalid. Names cannot contain leading or trailing whitespace.";

        foreach (var c in name)
        {
            if (!IsAllowedChar(c))
                return $"The {kind} is invalid. Names can only contain letters and spaces.";
        }

        if (name.Contains("  "))
            return $"The {kind} is invalid. Names cannot contain more than one space in a row.";

        return null;
    }
}
