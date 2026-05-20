using System.Text;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        [SerializeField, HideInInspector] internal TextMeshProUGUI helpContentText;

        private static string BuildHelpText()
        {
            var sb = new StringBuilder();

            sb.Append("syntax: <b>#key<noparse><op></noparse>value</b>   (free text also matches id / name / code)\n");
            sb.Append("combine multiple <noparse>#</noparse>predicates with spaces, all must match.\n");
            sb.Append("wrap a value in <noparse>\"</noparse> to match a phrase containing spaces.\n\n");

            sb.Append("<b>operators:</b>\n");
            sb.Append("=\n");
            sb.Append("!=\n");
            sb.Append("<noparse><</noparse>\n");
            sb.Append("<noparse><=</noparse>\n");
            sb.Append("<noparse>></noparse>\n");
            sb.Append("<noparse>>=</noparse>\n\n");

            AppendSection(sb, "monsters", MonsterPredicates);
            AppendSection(sb, "items", ItemPredicates);
            AppendSection(sb, "maps", MapPredicates);

            sb.Append("<b>examples:</b>\n");
            sb.Append("<color=#444444>#level>=50 #element=fire1</color>\n");
            sb.Append("<color=#444444>#tags=undead #race=demihuman</color>\n");
            sb.Append("<color=#444444>#itemclass=weapon #slots>=4</color>\n");
            sb.Append("<color=#444444>#description=<noparse>\"</noparse>fire bolt<noparse>\"</noparse></color>\n\n");

            sb.Append("<b>GM only:</b>\nRight click on monsters to spawn them.\nRight click on items to receive them.\nRight click on map location to warp there.");

            return sb.ToString();
        }

        private static void AppendSection(StringBuilder sb, string title, IPredicateRegistry reg)
        {
            sb.Append("<b>").Append(title).Append(":</b>\n");
            foreach (var key in reg.Keys)
            {
                sb.Append(key).Append('\n');
                if (reg.TryGetValues(key, out var values))
                {
                    foreach (var v in values)
                    {
                        sb.Append(" - ").Append(v).Append('\n');
                    }
                }
            }
            sb.Append('\n');
        }
    }
}
