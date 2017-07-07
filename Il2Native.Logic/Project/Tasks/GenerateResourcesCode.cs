﻿namespace Il2Native.Logic.Project.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.IO;
    using System.Xml.Linq;

    public class GenerateResourcesCode : Task
    {
        private enum TargetLanguage
        {
            CSharp,
            VB
        }

        private TargetLanguage _targetLanguage;

        private StreamWriter _targetStream;

        private StringBuilder _debugCode = new StringBuilder();

        private Dictionary<string, int> _keys;

        private string _resourcesName;

        public string ResxFilePath
        {
            get;
            set;
        }

        public string OutputSourceFilePath
        {
            get;
            set;
        }

        public string AssemblyName
        {
            get;
            set;
        }

        public bool DebugOnly
        {
            get;
            set;
        }

        public bool OmitResourceAccess
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                this._resourcesName = "FxResources." + this.AssemblyName;

                var path = Path.GetDirectoryName(this.OutputSourceFilePath);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                using (this._targetStream = File.CreateText(this.OutputSourceFilePath))
                {
                    if (string.Equals(Path.GetExtension(this.OutputSourceFilePath), ".vb", StringComparison.OrdinalIgnoreCase))
                    {
                        this._targetLanguage = GenerateResourcesCode.TargetLanguage.VB;
                    }
                    this._keys = new Dictionary<string, int>();
                    this.WriteClassHeader();
                    this.RunOnResFile();
                    this.WriteDebugCode();
                    this.WriteGetTypeProperty();
                    this.WriteClassEnd();
                    this.WriteResourceTypeClass();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to generate the resource code with error:\n" + ex.Message, Array.Empty<object>());
                return false;
            }

            return true;
        }

        private void WriteClassHeader()
        {
            string text = (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp) ? "// " : "' ";
            this._targetStream.WriteLine(text + "Do not edit this file manually it is auto-generated during the build based on the .resx file for this project.");
            if (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp)
            {
                this._targetStream.WriteLine("namespace System");
                this._targetStream.WriteLine("{");
                this._targetStream.WriteLine("    internal static partial class SR");
                this._targetStream.WriteLine("    {");
                this._targetStream.WriteLine("#pragma warning disable 0414");
                this._targetStream.WriteLine("        private const string s_resourcesName = \"{0}\";", this._resourcesName + ".SR");
                this._targetStream.WriteLine("#pragma warning restore 0414");
                this._targetStream.WriteLine("");
                if (!this.DebugOnly)
                {
                    this._targetStream.WriteLine("#if !DEBUGRESOURCES");
                    return;
                }
            }
            else
            {
                this._targetStream.WriteLine("Namespace System");
                this._targetStream.WriteLine("    Friend Partial Class SR");
                this._targetStream.WriteLine("    ");
                this._targetStream.WriteLine("        Private Const s_resourcesName As String = \"{0}\"", this._resourcesName + ".SR");
                this._targetStream.WriteLine("");
                if (!this.DebugOnly)
                {
                    this._targetStream.WriteLine("#If Not DEBUGRESOURCES Then");
                }
            }
        }

        private void RunOnResFile()
        {
            using (IEnumerator<KeyValuePair<string, string>> enumerator = GenerateResourcesCode.GetResources(this.ResxFilePath).GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    KeyValuePair<string, string> current = enumerator.Current;
                    this.StoreValues(current.Key, current.Value);
                }
            }
        }

        private void StoreValues(string leftPart, string rightPart)
        {
            int num;
            if (this._keys.TryGetValue(leftPart, out num))
            {
                return;
            }
            this._keys[leftPart] = 0;
            StringBuilder stringBuilder = new StringBuilder(rightPart.Length);
            for (int i = 0; i < rightPart.Length; i++)
            {
                if (rightPart[i] == '"' && (this._targetLanguage == GenerateResourcesCode.TargetLanguage.VB || this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp))
                {
                    stringBuilder.Append("\"");
                }

                stringBuilder.Append(rightPart[i]);
            }
            if (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp)
            {
                if (this.OmitResourceAccess)
                {
                    this._debugCode.AppendFormat("        internal static string {0} {2}{4}              get {2} return (@\"{1}\" ?? \"{0}\"); {3}{4}        {3}{4}", new object[]
                    {
                    leftPart,
                    stringBuilder.ToString(),
                    "{",
                    "}",
                    Environment.NewLine
                    });
                }
                else
                {
                    this._debugCode.AppendFormat("        internal static string {0} {2}{4}              get {2} return SR.GetResourceString(\"{0}\", @\"{1}\"); {3}{4}        {3}{4}", new object[]
                    {
                    leftPart,
                    stringBuilder.ToString(),
                    "{",
                    "}",
                    Environment.NewLine
                    });
                }
            }
            else
            {
                this._debugCode.AppendFormat("        Friend Shared ReadOnly Property {0} As String{2}            Get{2}                Return SR.GetResourceString(\"{0}\", \"{1}\"){2}            End Get{2}        End Property{2}", leftPart, stringBuilder.ToString(), Environment.NewLine);
            }
            if (!this.DebugOnly)
            {
                if (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp)
                {
                    if (this.OmitResourceAccess)
                    {
                        this._targetStream.WriteLine("        internal static string {0} {2}{4}              get {2} return (@\"{1}\" ?? \"{0}\"); {3}{4}        {3}", new object[]
                        {
                            leftPart,
                            stringBuilder.ToString(),
                            "{",
                            "}",
                            Environment.NewLine
                        });
                    }
                    else
                    {
                        this._targetStream.WriteLine("        internal static string {0} {2}{4}              get {2} return SR.GetResourceString(\"{0}\", {1}); {3}{4}        {3}", new object[]
                        {
                            leftPart,
                            "null",
                            "{",
                            "}",
                            Environment.NewLine
                        });
                    }
                    return;
                }
                this._targetStream.WriteLine("        Friend Shared ReadOnly Property {0} As String{2}           Get{2}                 Return SR.GetResourceString(\"{0}\", {1}){2}            End Get{2}        End Property", leftPart, "Nothing", Environment.NewLine);
            }
        }

        private void WriteDebugCode()
        {
            if (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp)
            {
                if (!this.DebugOnly)
                {
                    this._targetStream.WriteLine("#else");
                }
                this._targetStream.WriteLine(this._debugCode.ToString());
                if (!this.DebugOnly)
                {
                    this._targetStream.WriteLine("#endif");
                    return;
                }
            }
            else
            {
                if (!this.DebugOnly)
                {
                    this._targetStream.WriteLine("#Else");
                }
                this._targetStream.WriteLine(this._debugCode.ToString());
                if (!this.DebugOnly)
                {
                    this._targetStream.WriteLine("#End If");
                }
            }
        }

        private void WriteGetTypeProperty()
        {
            if (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp)
            {
                this._targetStream.WriteLine("        internal static Type ResourceType {1}{3}              get {1} return typeof({0}); {2}{3}        {2}", new object[]
                {
                    this._resourcesName + ".SR",
                    "{",
                    "}",
                    Environment.NewLine
                });
                return;
            }
            this._targetStream.WriteLine("        Friend Shared ReadOnly Property ResourceType As Type{1}           Get{1}                 Return GetType({0}){1}            End Get{1}        End Property", this._resourcesName + ".SR", Environment.NewLine);
        }

        private void WriteClassEnd()
        {
            if (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp)
            {
                this._targetStream.WriteLine("    }");
                this._targetStream.WriteLine("}");
                return;
            }
            this._targetStream.WriteLine("    End Class");
            this._targetStream.WriteLine("End Namespace");
        }

        private void WriteResourceTypeClass()
        {
            if (this._targetLanguage == GenerateResourcesCode.TargetLanguage.CSharp)
            {
                this._targetStream.WriteLine("namespace {0}", this._resourcesName);
                this._targetStream.WriteLine("{");
                this._targetStream.WriteLine("    // The type of this class is used to create the ResourceManager instance as the type name matches the name of the embedded resources file");
                this._targetStream.WriteLine("    internal static class SR");
                this._targetStream.WriteLine("    {");
                this._targetStream.WriteLine("    }");
                this._targetStream.WriteLine("}");
                return;
            }
            this._targetStream.WriteLine("Namespace {0}", this._resourcesName);
            this._targetStream.WriteLine("    ' The type of this class is used to create the ResourceManager instance as the type name matches the name of the embedded resources file");
            this._targetStream.WriteLine("    Friend Class SR");
            this._targetStream.WriteLine("    ");
            this._targetStream.WriteLine("    End Class");
            this._targetStream.WriteLine("End Namespace");
        }

        internal static IEnumerable<KeyValuePair<string, string>> GetResources(string fileName)
        {
            XDocument xDocument = XDocument.Load(fileName, LoadOptions.PreserveWhitespace);
            using (IEnumerator<XElement> enumerator = xDocument.Element("root").Elements("data").GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    XElement current = enumerator.Current;
                    string value = current.Attribute("name").Value;
                    string value2 = current.Element("value").Value;
                    yield return new KeyValuePair<string, string>(value, value2);
                }
            }
        }
    }
}
