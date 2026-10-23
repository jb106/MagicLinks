#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MagicLinks
{
    /// <summary>
    /// Generates Assets/Resources/MagicLinks/Network/MagicLinksNetwork.cs from the networked links.
    /// Fully separate from MagicLinksScriptsGenerator: the non-network scripts never depend on it.
    /// </summary>
    public static class MagicLinksNetworkScriptsGenerator
    {
        public static string NetworkScriptPath =>
            Path.Combine(MagicLinksConst.NetworkPath, MagicLinksConst.NetworkScriptName + ".cs").Replace("\\", "/");

        public static void GenerateNetworkScript()
        {
            string content = GetNetworkScriptContent(out MagicLinksNetworkAnalysis analysis);

            if (content == null)
            {
                DeleteNetworkScript();
                return;
            }

            foreach (string error in analysis.Errors)
                Debug.LogError($"[MagicLinks] Network: {error}");

            if (File.Exists(NetworkScriptPath) && File.ReadAllText(NetworkScriptPath) == content) return;

            if (Directory.Exists(MagicLinksConst.NetworkPath) == false)
                Directory.CreateDirectory(MagicLinksConst.NetworkPath);

            File.WriteAllText(NetworkScriptPath, content);
            AssetDatabase.ImportAsset(NetworkScriptPath);
        }

        public static void DeleteNetworkScript()
        {
            if (File.Exists(NetworkScriptPath) == false) return;

            AssetDatabase.DeleteAsset(NetworkScriptPath);
        }

        /// <summary>Returns null when there is nothing to generate (no netcode package or no networked link).</summary>
        public static string GetNetworkScriptContent(out MagicLinksNetworkAnalysis analysis)
        {
            analysis = null;

            if (MagicLinksNetcodeSupport.IsNetcodeInstalled == false) return null;

            List<DynamicVariable> variables = MagicLinksInternalVar.GetExistingVariables();
            if (variables.Any(v => v.isNetworked) == false) return null;

            analysis = MagicLinksNetworkAnalyzer.Analyze(variables, MagicLinksUtilities.GetConfiguration());

            string template = File.ReadAllText(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.MagicNetworkTemplate));
            return BuildNetworkScript(template, analysis);
        }

        public static string BuildNetworkScript(string template, MagicLinksNetworkAnalysis analysis)
        {
            int start = template.IndexOf("/*", StringComparison.Ordinal) + 2;
            int end = template.LastIndexOf("*/", StringComparison.Ordinal);
            string content = template.Substring(start, end - start).TrimStart('\r', '\n');

            if (analysis.HasErrors)
            {
                // Nothing is synchronized, and the errors are reported again when a session starts.
                content = content.Replace("#REGISTRATION", "null");
                content = content.Replace("#ERRORS", GetStringArray(analysis.Errors));
                content = content.Replace("//NETWORKLINKS", string.Empty);
                content = content.Replace("//NETWORKSERIALIZERS", string.Empty);
            }
            else
            {
                content = content.Replace("#REGISTRATION", "Register");
                content = content.Replace("#ERRORS", "null");
                content = content.Replace("//NETWORKLINKS", GetLinksRegistration(analysis));
                content = content.Replace("//NETWORKSERIALIZERS", GetSerializers(analysis));
            }

            // The hash covers everything that defines the protocol: a client and a server built from
            // different links or different types refuse to talk to each other.
            uint hash = Fnv1a(content);
            return content.Replace("#SCHEMAHASH", $"0x{hash:X8}u");
        }

        private static string GetLinksRegistration(MagicLinksNetworkAnalysis analysis)
        {
            StringBuilder sb = new StringBuilder();

            foreach (MagicNetLinkDescription link in analysis.links)
            {
                DynamicVariable v = link.variable;
                string name = ToLiteral(v.vName);

                if (v.IsVoid())
                    sb.AppendLine($"            session.AddVoidEvent({link.id}, {name}, manager.{link.dictionary});");
                else if (v.IsEvent())
                    sb.AppendLine($"            session.AddEvent({link.id}, {name}, manager.{link.dictionary}, {link.serializer});");
                else
                    sb.AppendLine($"            session.{(v.isList ? "AddList" : "AddVariable")}({link.id}, {name}, manager.{link.dictionary}, " +
                                  $"{link.serializer}, {(v.resetOnWriterDisconnect ? "true" : "false")});");
            }

            return sb.ToString().TrimEnd();
        }

        private static string GetSerializers(MagicLinksNetworkAnalysis analysis)
        {
            StringBuilder sb = new StringBuilder();

            foreach (MagicNetTypeDescription description in analysis.types)
            {
                AppendSerializer(sb, description);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendSerializer(StringBuilder sb, MagicNetTypeDescription d)
        {
            string t = MagicLinksNetworkAnalyzer.GetCSharpName(d.type);
            string c = d.serializerClassName;
            bool isClass = d.type.IsValueType == false;

            List<MagicNetFieldDescription> networked = d.fields.Where(f => f.serializer != null).ToList();
            List<MagicNetFieldDescription> copyable = d.fields.Where(f => f.field.IsInitOnly == false).ToList();

            sb.AppendLine($"    public sealed class {c} : MagicNetSerializer<{t}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static readonly {c} Instance = new {c}();");
            sb.AppendLine();

            for (int i = 0; i < networked.Count; i++)
            {
                string fieldType = MagicLinksNetworkAnalyzer.GetCSharpName(networked[i].field.FieldType);
                sb.AppendLine($"        private static readonly MagicNetSerializer<{fieldType}> S{i} = {networked[i].serializer};");
            }

            if (networked.Count > 0) sb.AppendLine();

            // Write
            sb.AppendLine($"        public override void Write(FastBufferWriter writer, {t} value)");
            sb.AppendLine("        {");
            if (isClass) sb.AppendLine("            if (WriteNullFlag(writer, value) == false) return;");
            for (int i = 0; i < networked.Count; i++)
                sb.AppendLine($"            S{i}.Write(writer, value.@{networked[i].field.Name});");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Read: excluded fields come from the local value
            sb.AppendLine($"        public override {t} Read(FastBufferReader reader, {t} local)");
            sb.AppendLine("        {");
            if (isClass)
            {
                sb.AppendLine("            if (ReadNullFlag(reader) == false) return null;");
                sb.AppendLine("            bool hasLocal = local is not null;");
                sb.AppendLine($"            {t} value = hasLocal ? Clone(local) : new {t}();");
                for (int i = 0; i < networked.Count; i++)
                {
                    string f = "@" + networked[i].field.Name;
                    sb.AppendLine($"            value.{f} = S{i}.Read(reader, hasLocal ? local.{f} : default);");
                }
            }
            else
            {
                sb.AppendLine($"            {t} value = local;");
                for (int i = 0; i < networked.Count; i++)
                {
                    string f = "@" + networked[i].field.Name;
                    sb.AppendLine($"            value.{f} = S{i}.Read(reader, local.{f});");
                }
            }
            sb.AppendLine("            return value;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // AreEqual: only networked fields
            sb.AppendLine($"        public override bool AreEqual({t} a, {t} b)");
            sb.AppendLine("        {");
            if (isClass)
            {
                sb.AppendLine("            if (ReferenceEquals(a, b)) return true;");
                sb.AppendLine("            if (a is null || b is null) return false;");
            }
            string comparison = networked.Count == 0
                ? "true"
                : string.Join("\n                && ", networked.Select((f, i) => $"S{i}.AreEqual(a.@{f.field.Name}, b.@{f.field.Name})"));
            sb.AppendLine($"            return {comparison};");
            sb.AppendLine("        }");
            sb.AppendLine();

            // WritePatch: a bit per networked field that changed, then only those fields
            sb.AppendLine($"        public override void WritePatch(FastBufferWriter writer, {t} from, {t} to)");
            sb.AppendLine("        {");
            sb.AppendLine(isClass
                ? "            bool full = from is null || to is null;"
                : "            bool full = false;");
            sb.AppendLine("            writer.WriteValueSafe(full);");
            sb.AppendLine("            if (full)");
            sb.AppendLine("            {");
            sb.AppendLine("                Write(writer, to);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            ulong mask = 0;");
            for (int i = 0; i < networked.Count; i++)
            {
                string f = "@" + networked[i].field.Name;
                sb.AppendLine($"            if (S{i}.AreEqual(from.{f}, to.{f}) == false) mask |= 1UL << {i};");
            }
            sb.AppendLine("            writer.WriteValueSafe(mask);");
            for (int i = 0; i < networked.Count; i++)
                sb.AppendLine($"            if ((mask & (1UL << {i})) != 0) S{i}.Write(writer, to.@{networked[i].field.Name});");
            sb.AppendLine("        }");
            sb.AppendLine();

            // ReadPatch: applied on top of the authoritative value
            sb.AppendLine($"        public override {t} ReadPatch(FastBufferReader reader, {t} authoritative)");
            sb.AppendLine("        {");
            sb.AppendLine("            reader.ReadValueSafe(out bool full);");
            sb.AppendLine("            if (full) return Read(reader, authoritative);");
            sb.AppendLine();
            sb.AppendLine("            reader.ReadValueSafe(out ulong mask);");
            sb.AppendLine(isClass
                ? $"            {t} value = authoritative is not null ? Clone(authoritative) : new {t}();"
                : $"            {t} value = authoritative;");
            for (int i = 0; i < networked.Count; i++)
            {
                string f = "@" + networked[i].field.Name;
                sb.AppendLine($"            if ((mask & (1UL << {i})) != 0) value.{f} = S{i}.Read(reader, value.{f});");
            }
            sb.AppendLine("            return value;");
            sb.AppendLine("        }");

            if (isClass)
            {
                sb.AppendLine();
                sb.AppendLine($"        private static {t} Clone({t} source)");
                sb.AppendLine("        {");
                sb.AppendLine($"            {t} copy = new {t}();");
                foreach (MagicNetFieldDescription f in copyable)
                    sb.AppendLine($"            copy.@{f.field.Name} = source.@{f.field.Name};");
                sb.AppendLine("            return copy;");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
        }

        //----------------------------------------

        private static string GetStringArray(IEnumerable<string> values) =>
            "new string[] { " + string.Join(", ", values.Select(ToLiteral)) + " }";

        private static string ToLiteral(string value)
        {
            StringBuilder sb = new StringBuilder("\"");

            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    default: sb.Append(ch); break;
                }
            }

            return sb.Append('"').ToString();
        }

        private static uint Fnv1a(string s)
        {
            uint hash = 2166136261;

            foreach (char ch in s.Replace("\r\n", "\n"))
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return hash;
        }
    }
}
#endif
