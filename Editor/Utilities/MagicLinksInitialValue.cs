using System.Globalization;
using UnityEngine;

namespace MagicLinks
{
    public static class MagicLinksInitialValue
    {
        // Only base value types — no object refs, no lists, no events.
        public static bool IsSupported(string label, bool isList, int magicType)
        {
            if (isList) return false;
            if (magicType != 0) return false;

            switch (label)
            {
                case MagicLinksConst.String:
                case MagicLinksConst.Bool:
                case MagicLinksConst.Int:
                case MagicLinksConst.Float:
                case MagicLinksConst.Vector2:
                case MagicLinksConst.Vector3:
                case MagicLinksConst.Color:
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryParse(string label, string raw, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(raw)) return false;

            try
            {
                switch (label)
                {
                    case MagicLinksConst.String:
                        value = raw; return true;
                    case MagicLinksConst.Bool:
                        if (bool.TryParse(raw, out var b)) { value = b; return true; }
                        return false;
                    case MagicLinksConst.Int:
                        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; }
                        return false;
                    case MagicLinksConst.Float:
                        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) { value = f; return true; }
                        return false;
                    case MagicLinksConst.Vector2:
                        return TryParseVector2(raw, out value);
                    case MagicLinksConst.Vector3:
                        return TryParseVector3(raw, out value);
                    case MagicLinksConst.Color:
                        if (ColorUtility.TryParseHtmlString(raw, out var c)) { value = c; return true; }
                        return false;
                }
            }
            catch
            {
                // fall through
            }
            return false;
        }

        public static string Format(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case float f: return f.ToString(CultureInfo.InvariantCulture);
                case Vector2 v2:
                    return v2.x.ToString(CultureInfo.InvariantCulture) + ";" +
                           v2.y.ToString(CultureInfo.InvariantCulture);
                case Vector3 v3:
                    return v3.x.ToString(CultureInfo.InvariantCulture) + ";" +
                           v3.y.ToString(CultureInfo.InvariantCulture) + ";" +
                           v3.z.ToString(CultureInfo.InvariantCulture);
                case Color c:
                    return "#" + ColorUtility.ToHtmlStringRGBA(c);
            }
            return value.ToString();
        }

        private static bool TryParseVector2(string raw, out object value)
        {
            value = null;
            string[] parts = raw.Split(';');
            if (parts.Length != 2) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            value = new Vector2(x, y);
            return true;
        }

        private static bool TryParseVector3(string raw, out object value)
        {
            value = null;
            string[] parts = raw.Split(';');
            if (parts.Length != 3) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return false;
            value = new Vector3(x, y, z);
            return true;
        }
    }
}
