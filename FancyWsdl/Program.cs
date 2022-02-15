using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using FancyWsdl.Extensions;

namespace FancyWsdl
{
    public class Program
    {
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        private static void Run(string fileName, string args, string path)
        {
            var info = new ProcessStartInfo(fileName, args);
            info.WorkingDirectory = path;
            Process.Start(info)?.WaitForExit();
        }

        static string PascalCase(string s) => s;// Regex.Replace(NormalizeName(s), @"(^|\s+)(?<letter>\S)", m => m.Groups["letter"].Value.ToUpper());
        static string modPascalCase(string s) => Regex.Replace(NormalizeClassesName(s), @"(^|\s+)(?<letter>\S)", m => m.Groups["letter"].Value.ToUpper());

        // Thx https://github.com/Kaupisch-IT/FancyWsdl
        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(AssemblyDirectory);

            Console.WriteLine("\nПрограмма FancyWsdl адаптированная под ГИИС ДМДК - http://dmdk.ru\n");

            if (args.Length != 3)
            {
                if (ConfigurationManager.AppSettings.HasKeys()
                    && ConfigurationManager.AppSettings["CSFileName"].TryGetNotNullOrEmpty(out var csName)
                    && ConfigurationManager.AppSettings["Namespace"].TryGetNotNullOrEmpty(out var nsName)
                    && ConfigurationManager.AppSettings["WSDLscheme"].TryGetNotNullOrEmpty(out var wsdlName))
                {
                    args = new[] { csName, nsName, wsdlName };
                }
                else
                {
                    Console.WriteLine("Программа требует заполнение .app.Settings или 3 аргумента программы например: FancyWsdl.exe ИмяФайла.cs ОбластьИменCsФайла ИмяИлиАдресСхемыWsdl\n");
                    return;
                }

            }

            var cfName = "ServiceReference\\" + args[0];
            if (File.Exists(cfName))
                File.Delete(cfName);

            //  /importXmlTypes /messageContract  /tcv:Version35  /out={args[0]}
            // --noTypeReuse 
            var arg = $"{args[2]}  --noTypeReuse  -n \"*,{args[1]}\" -d \"ServiceReference\" -o \"{args[0]}\" -tf \"netstandard2.0\"";
            Console.WriteLine($"Running> dotnet-svcutil {arg}");

            Run("dotnet-svcutil", arg, Directory.GetCurrentDirectory());

            args[0] = cfName;



            if (args.Any())
            {
                var path = args[0];

                var encoding = Encoding.UTF8;
                var fileContent = File.ReadAllText(path, encoding);
                fileContent=fileContent.Replace("[System.Xml.Serialization.XmlElement]", "[System.Xml.Serialization.XmlElement()]");
                fileContent=fileContent.Replace("[System.Xml.Serialization.XmlAttribute]", "[System.Xml.Serialization.XmlAttribute()]");
                fileContent=fileContent.Replace("[System.Xml.Serialization.XmlAttributeAttribute]", "[System.Xml.Serialization.XmlAttributeAttribute()]");
                fileContent=fileContent.Replace("[System.Xml.Serialization.XmlElementAttribute]", "[System.Xml.Serialization.XmlElement()]");

                Console.WriteLine($"Форматирование файла: {path}");
                bool anotaded=false;
       
                // enumerate all class definitions
                foreach (Match classMatch in Regex.Matches(fileContent, @"^(?<space>\s*)public partial class (?<className>\S+).*?\n(\k<space>)}", RegexOptions.Singleline | RegexOptions.Multiline))
                {
                    var className = classMatch.Groups["className"].Value;
                    var classContent = classMatch.Value;

                    // property name in XmlElementAttribute    
                    //foreach (Match match in Regex.Matches(classContent, @"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlElement(Attribute)?\()(""(?<elementName>[^""]+)"")?[^\)]*\)\]\s+public (?<propertyType>\S+) (?<propertyName>\S+)"))
                    foreach (Match match in Regex.Matches(classContent, @"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlElement(Attribute)?\()(""(?<elementName>[^""]+)"")?[^\)]*\)\]\s+public (?<propertyType>\S+) (?<propertyName>\S+)"))
                    {
                        var xmlElementAttribute = match.Groups["xmlElementAttribute"].Value;
                        var elementName = match.Groups["elementName"].Value;
                        var propertyType = match.Groups["propertyType"].Value;
                        var propertyName = match.Groups["propertyName"].Value;
                        if (string.IsNullOrEmpty(elementName))
                        {
                            classContent = classContent.Replace(match.Value, match.Value.Replace(xmlElementAttribute, xmlElementAttribute + "\"" + propertyName + "\", "))
                                .Replace(", )", ")");
                            
                        }
                    }

                    // property name in XmlElementAttribute    
                    //foreach (Match match in Regex.Matches(classContent, @"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlElement(Attribute)?\()(""(?<elementName>[^""]+)"")?[^\)]*\)\]\s+public (?<propertyType>\S+) (?<propertyName>\S+)"))
                    foreach (Match match in Regex.Matches(classContent, @"\[(?<xmlAttribute>(System.Xml.Serialization.)?XmlAttribute(Attribute)?\()(""(?<elementName>[^""]+)"")?[^\)]*\)\]\s+public (?<propertyType>\S+) (?<propertyName>\S+)"))
                    {
                        var xmlElementAttribute = match.Groups["xmlAttribute"].Value;
                        var elementName = match.Groups["elementName"].Value;
                        var propertyType = match.Groups["propertyType"].Value;
                        var propertyName = match.Groups["propertyName"].Value;
                        //  if (string.IsNullOrEmpty(elementName))
                        classContent = classContent.Replace(match.Value, match.Value.Replace(xmlElementAttribute, xmlElementAttribute + "\"" + propertyName + "\", "))
                            .Replace(", )", ")");;
                    }



                    // auto-implemented getters & setters
                    foreach (Match match in Regex.Matches(classContent, @"public (?<propertyType>\S+) (?<propertyName>\S+)(?<getterSetter>\s+\{\s+get\s+\{\s+return this\.(?<fieldName>[^;]+);\s+}\s+set\s+\{\s+[^;]+;\s+\}\s+\})"))
                    {
                        var propertyType = match.Groups["propertyType"].Value;
                        var propertyName = match.Groups["propertyName"].Value;
                        var getterSetter = match.Groups["getterSetter"].Value;
                        var fieldName = match.Groups["fieldName"].Value;

                        classContent = classContent.Replace(match.Value, match.Value.Replace(getterSetter, " { get; set; }"));
                        classContent = Regex.Replace(classContent, $@"private {Regex.Escape(propertyType)} {Regex.Escape(fieldName)};\s*", "");
                        classContent = Regex.Replace(classContent, $@"\b{Regex.Escape(fieldName)}\b", propertyName);
                    }

                    // property names with uppercase first letter
                    foreach (Match match in Regex.Matches(classContent, @"(?<pre>public \S+ )(?<propertyName>\S+)(?<post>\s+\{)"))
                    {
                        var pre = match.Groups["pre"].Value;
                        var propertyName = match.Groups["propertyName"].Value;
                        var post = match.Groups["post"].Value;

                        var newPropertyName = PascalCase(propertyName);

                        if (newPropertyName != propertyName)
                            pre = $"[System.Xml.Serialization.XmlElementAttribute(ElementName = \"{propertyName}\")]\n\t\t" + pre;

                        classContent = classContent.Replace(match.Value, pre + newPropertyName + post);
                        classContent = classContent.Replace($"this.{propertyName} ", $"this.{newPropertyName} ");
                        classContent = classContent.Replace($@".SoapHeaderAttribute(""{propertyName}"")", $@".SoapHeaderAttribute(""{newPropertyName}"")");
                    }

                    // compute *Specified properties
                    foreach (Match match in Regex.Matches(classContent, @"\[(System.Xml.Serialization.)?XmlIgnore(Attribute)?\(\)\]\s+public bool (?<propertyName>\S+)Specified (?<getterSetter>\{ get; set; \}|\{\s+get \s+[^;]+;\s+\}\s+\s+set \{\s+[^;]+;\s+\}\s+\})"))
                    {
                        var propertyName = match.Groups["propertyName"].Value;
                        var getterSetter = match.Groups["getterSetter"].Value;

                        classContent = Regex.Replace(classContent, $@"(?<pre>public \S+)(?<post> {propertyName} )", m => m.Groups["pre"].Value + "?" + m.Groups["post"].Value);
                        classContent = classContent.Replace(match.Value, match.Value.Replace(getterSetter, $"=> this.{modPascalCase(propertyName)}.HasValue;"));
//\                        classContent = classContent.Replace(match.Value, pre + newPropertyName + post);
                    }

                    // method name in SoapDocumentMethodAttribute
                    foreach (Match match in Regex.Matches(classContent, @"(?<soapDocumentMethodAttribute>\[(System.Web.Services.Protocols.)?SoapDocumentMethod(Attribute)?\(""[^""]*""[^\]]*)\)\]\s*\[return: [^]]+\]\s+public \S+ (?<methodName>[^\s\(]+)\("))
                    {
                        var soapDocumentMethodAttribute = match.Groups["soapDocumentMethodAttribute"].Value;
                        var methodName = match.Groups["methodName"].Value;

                        string argumentsToAdd = null;
                        if (!soapDocumentMethodAttribute.Contains("RequestElementName = "))
                            argumentsToAdd += $", RequestElementName = \"{methodName}\"";
                        if (!soapDocumentMethodAttribute.Contains("ResponseElementName = "))
                            argumentsToAdd += $", ResponseElementName = \"{methodName}Response\"";
                        classContent = classContent.Replace(match.Value, match.Value.Replace(soapDocumentMethodAttribute, soapDocumentMethodAttribute + argumentsToAdd));
                    }

                    // method name with uppercase first letter
                    foreach (Match match in Regex.Matches(classContent, @"(?<pre>public \S+ (Begin|End)?)(?<methodName>[^\s\(]+)(?<inter>(Async)?[^\n]*(\n[^\n]*){1,4}this\.(Begin|End)?Invoke(Async)?\()(""(?<methodName2>[^""]+)"")?(?<post>)"))
                    {
                        var pre = match.Groups["pre"].Value;
                        var methodName = match.Groups["methodName"].Value;
                        var inter = match.Groups["inter"].Value;
                        var methodName2 = match.Groups["methodName2"].Value;
                        var post = match.Groups["post"].Value;

                        classContent = classContent.Replace(match.Value, pre + PascalCase(methodName) + inter + ((!string.IsNullOrEmpty(methodName2)) ? "nameof(" + PascalCase(methodName2) + ")" : "") + post);
                    }
             
                    foreach (Match match in Regex.Matches(classContent, @"public void (?<methodName>[^\s\(]+)"))
                    {
                        var methodName = match.Groups["methodName"].Value;
                        classContent = Regex.Replace(classContent, $@"(?<pre>( |\.)){methodName}(?<post>[\(\)])", m => m.Groups["pre"].Value + PascalCase(methodName) + m.Groups["post"].Value);
                    }

                    fileContent = fileContent.Replace(classMatch.Value, classContent);
                }

                // enumerate all enum definitions
                foreach (Match enumMatch in Regex.Matches(fileContent, @"^(?<space>\s*)public enum (?<enumName>\S+).*?\n(\k<space>)}", RegexOptions.Singleline | RegexOptions.Multiline))
                {
                    var enumName = enumMatch.Groups["enumName"].Value;
                    var enumContent = enumMatch.Value;

                    // enumerate all enum values
                    foreach (Match valueMatch in Regex.Matches(enumContent, @"(\[(?<xmlEnumAttribute>(System.Xml.Serialization.)?XmlEnum(Attribute)?\()(""(?<enumValueName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)(?<enumValue>\S+),"))
                    {
                        var xmlEnumAttribute = valueMatch.Groups["xmlEnumAttribute"].Value;
                        var enumValueName = valueMatch.Groups["enumValueName"].Value;
                        var space = valueMatch.Groups["space"].Value;
                        var enumValue = valueMatch.Groups["enumValue"].Value;

                        // enum values in XmlEnumAttribute
                        if (string.IsNullOrEmpty(xmlEnumAttribute))
                            enumContent = enumContent.Replace(valueMatch.Value, space + $"[System.Xml.Serialization.XmlEnumAttribute(\"{enumValue}\")]" + valueMatch.Value);

                        // enum value with uppercase first letter
                        enumContent = Regex.Replace(enumContent, @$"\b{enumValue},", $"{PascalCase((Regex.IsMatch(enumValueName, @"^[a-zA-Z_][\w\s]*$")) ? enumValueName : enumValue)},");
                    }
                    fileContent = fileContent.Replace(enumMatch.Value, enumContent);
                }

                // enumerate all type definitions
                foreach (Match classMatch in Regex.Matches(fileContent, @"(\[(?<xmlRootAttribute>(System.Xml.Serialization.)?XmlRoot(Attribute)?\()(""(?<rootName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)(?<classDefinition>public (partial class|enum) (?<className>\S+))"))
                {
                    var xmlRootAttribute = classMatch.Groups["xmlRootAttribute"].Value;
                    var rootName = classMatch.Groups["rootName"].Value;
                    var space = classMatch.Groups["space"].Value;
                    var classDefinition = classMatch.Groups["classDefinition"].Value;
                    var className = classMatch.Groups["className"].Value;

                    // type name in XmlRootAttribute
                    if (string.IsNullOrEmpty(xmlRootAttribute))
                        fileContent = Regex.Replace(fileContent, $@"{Regex.Escape(classMatch.Value)}\b", classMatch.Value.Replace(classDefinition, $"[System.Xml.Serialization.XmlRootAttribute(\"{className}\")]" + space + classDefinition));
                    else if (string.IsNullOrEmpty(rootName))
                        fileContent = Regex.Replace(fileContent, $@"{Regex.Escape(classMatch.Value)}\b", classMatch.Value.Replace(xmlRootAttribute, xmlRootAttribute + "\"" + className + "\", "));

                    // type name with uppercase first letter
                    fileContent = Regex.Replace(fileContent, $@"(?<!"")\b{Regex.Escape(className)}\b(?!""|(\(\[))", PascalCase(className));
                }

                // // use usings
                //var usings = Regex.Matches(fileContent, @"(?<namespace>System(\.\w+)*)\.\w+(?=\([^\]]*\)]| \w+ = |<| )|((?<namespace>System(\.\w+)*)(\.\w+){2},)|(using (?<namespace>\S+);)").AsList().Select(m => m.Groups["namespace"].Value.Trim('.')).Distinct().ToArray();
                // foreach (var usingNamespace in usings.OrderByDescending(u => u.Length))
                //     fileContent = fileContent.Replace(usingNamespace + ".", "");
                // var usingDeclaration = string.Join(Environment.NewLine, usings.OrderBy(u => u).Select(u => $"using {u};")) + Environment.NewLine;
                // if (Regex.IsMatch(fileContent, @"using \S+;"))
                //     fileContent = Regex.Replace(fileContent, @"(using \S+;\s?\n)+", usingDeclaration);
                // else
                //     fileContent = usingDeclaration + Environment.NewLine + fileContent;


                // use attribute shortcut
                fileContent = fileContent.Replace("Attribute()]", "]");
                fileContent = fileContent.Replace("Attribute(", "(");


                fileContent =AddAnnotations(args, fileContent);

                fileContent = Fix(fileContent);

                fileContent = fileContent.Replace("public object Item { get; set; }", "[System.Text.Json.Serialization.JsonConverter(typeof(JWR.GiisDmdk.Sdk.Converters.ObjectTypedJsonConverter))]\n\t\tpublic object Item { get; set; }");


                File.WriteAllText(path, fileContent, encoding);
            }

            Console.WriteLine("Готово");
        }

        public static string Fix(string fileContent)
        {

            Console.WriteLine("Финишное форматирование....");
            // enumerate all class definitions
            foreach (Match classMatch in Regex.Matches(fileContent, @"^(?<space>\s*)public partial class (?<className>\S+).*?\n(\k<space>)}", RegexOptions.Singleline | RegexOptions.Multiline))
            {
                var className = classMatch.Groups["className"].Value;
                var classContent = classMatch.Value;

                // property names with uppercase first letter
                foreach (Match match in Regex.Matches(classContent, @"(?<pre>public \S+ )(?<propertyName>\S+)(?<post>\s+\{)"))
                {
                    var pre = match.Groups["pre"].Value;
                    var propertyName = match.Groups["propertyName"].Value;
                    var post = match.Groups["post"].Value;

                    var newPropertyName = modPascalCase(propertyName);
                    if (className == newPropertyName)
                        newPropertyName += "Prop";

                    classContent = classContent.Replace(match.Value, pre + newPropertyName + post);
                    classContent = classContent.Replace($"this.{propertyName} ", $"this.{newPropertyName} ");
                    classContent = classContent.Replace($@".SoapHeaderAttribute(""{propertyName}"")", $@".SoapHeaderAttribute(""{newPropertyName}"")");
                }

                // property names with uppercase first letter
                foreach (Match match in Regex.Matches(classContent, @"(?<pre>public \S+ )(?<propertyName>\S+)(?<post>\s+=>)"))
                {
                    var pre = match.Groups["pre"].Value;
                    var propertyName = match.Groups["propertyName"].Value;
                    var post = match.Groups["post"].Value;

                    var newPropertyName = modPascalCase(propertyName);
                    if (className == newPropertyName)
                        newPropertyName += "Prop";

                    classContent = classContent.Replace(match.Value, pre + newPropertyName + post);
                    classContent = classContent.Replace($"this.{propertyName} ", $"this.{newPropertyName} ");
                    classContent = classContent.Replace($@".SoapHeaderAttribute(""{propertyName}"")", $@".SoapHeaderAttribute(""{newPropertyName}"")");
                }

                fileContent = fileContent.Replace(classMatch.Value, classContent);
            }

            // enumerate all type definitions
            foreach (Match classMatch in Regex.Matches(fileContent, @"(\[(?<xmlRootAttribute>(System.Xml.Serialization.)?XmlRoot(Attribute)?\()(""(?<rootName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)(?<classDefinition>public (partial class|enum) (?<className>\S+))"))
            {
                var xmlRootAttribute = classMatch.Groups["xmlRootAttribute"].Value;
                var rootName = classMatch.Groups["rootName"].Value;
                var space = classMatch.Groups["space"].Value;
                var classDefinition = classMatch.Groups["classDefinition"].Value;
                var className = classMatch.Groups["className"].Value;

                // type name in XmlRootAttribute
                if (string.IsNullOrEmpty(xmlRootAttribute))
                    fileContent = Regex.Replace(fileContent, $@"{Regex.Escape(classMatch.Value)}\b", classMatch.Value.Replace(classDefinition, $"[System.Xml.Serialization.XmlRootAttribute(\"{className}\")]" + space + classDefinition));
                else if (string.IsNullOrEmpty(rootName))
                    fileContent = Regex.Replace(fileContent, $@"{Regex.Escape(classMatch.Value)}\b", classMatch.Value.Replace(xmlRootAttribute, xmlRootAttribute + "\"" + className + "\", "));

                // type name with uppercase first letter
                fileContent = Regex.Replace(fileContent, $@"(?<!"")\b{Regex.Escape(className)}\b(?!""|(\(\[))", modPascalCase(className));
            }

            return fileContent;
        }

        /// <summary>
        ///   add annotations/documentation from XML schema
        /// </summary>
        /// <param name="args"></param>
        /// <param name="fileContent"></param>
        /// <returns></returns>
        private static string AddAnnotations(string[] args, string fileContent)
        {
            foreach (var schemaUrl in args.Skip(2))
            {
                Console.WriteLine($"Документирование файла: {schemaUrl}");

                var xmlDocument = new XmlDocument();
                using (var xmlTextReader = new XmlTextReader(schemaUrl))
                {
                    xmlTextReader.Namespaces = false;
                    xmlDocument.Load(xmlTextReader);
                }

                // enumerate all class & enum definitions
                foreach (Match classMatch in Regex.Matches(fileContent,
                    @"(\[(?<xmlRootAttribute>(System.Xml.Serialization.)?XmlRoot(Attribute)?\()(""(?<rootName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)public (partial class|enum) (?<className>\S+).*?(\k<space>)}",
                    RegexOptions.Singleline | RegexOptions.Multiline))
                {
                    var className = classMatch.Groups["className"].Value;
                    var classContent = classMatch.Value;
                    var rootName = classMatch.Groups["rootName"].Value;

                    static string ToSummary(string text, string indentSpace)
                    {
                        var lines = text.Trim().Replace("\r", "").Split('\n');
                        for (var i = 0; i < lines.Length; i++)
                        {
                            lines[i] = lines[i].Trim();
                        }

                        text = HttpUtility.HtmlEncode(string.Join("\n", lines).Trim());
                        return text.Contains("\n") ? $"/// <summary>{indentSpace}/// {Regex.Replace(text, "\r?\n", indentSpace + "/// ")}{indentSpace}/// </summary>"
                            : $"/// <summary> {text} </summary>";
                    }

                    // element documentation
                    foreach (Match elementMatch in Regex.Matches(classContent,
                        @"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlElement(Attribute)?\()(""(?<elementName>[^""]+)"")?[^\)]*\)\](?<space>\s+)public (?<propertyType>\S+) (?<propertyName>\S+)"))
                    {
                        var propertyName = elementMatch.Groups["propertyName"].Value;
                        var elementName = elementMatch.Groups["elementName"].Value;

                        var elementDocumentation = GetElementDocumentation(xmlDocument, rootName, GetNotEmptyString(elementName, propertyName));

                        //elementDocumentation ??= GetClassDocumentation(xmlDocument, rootName, qry);
                        //string elementDocumentation = xmlDocument
                        //    .SelectSingleNode(
                        //        $"/*/*[@name='{rootName}']//*[@name='{elementName}']//*[contains(local-name(),'documentation')]")
                        //    ?.InnerText;

                        if (elementDocumentation != null)
                        {
                            elementDocumentation = ToSummary(elementDocumentation, elementMatch.Groups["space"].Value);
                            classContent = Regex.Replace(classContent,
                                @$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*public (?<propertyType>\S+) {propertyName}\b)",
                                m => elementDocumentation + m.Groups["remainder"].Value,
                                RegexOptions.Singleline | RegexOptions.Multiline);
                        }
                    }

                    // operation documentation
                    foreach (Match elementMatch in Regex.Matches(classContent,
                        @"\[(?<soapDocumentMethodAttribute>(System.Web.Services.Protocols.)?SoapDocumentMethod(Attribute)?\()(""(?<soapName>[^""]+)"")?[^\)]*\)\](\s*\[[^\n]+\])*(?<space>\s+)public (?<returnType>\S+) (?<methodName>[^(\s]+)\("))
                    {
                        var methodName = elementMatch.Groups["methodName"].Value;
                        var soapName = elementMatch.Groups["soapName"].Value;
                        var typeName = xmlDocument
                            .SelectSingleNode(
                                $"//*[contains(local-name(),'binding')]/*[contains(local-name(),'operation')]/*[@soapAction='{soapName}']/../../@type")
                            ?.InnerText;
                        var operationName = xmlDocument
                            .SelectSingleNode(
                                $"//*[contains(local-name(),'binding')]/*[contains(local-name(),'operation')]/*[@soapAction='{soapName}']/../@name")
                            ?.InnerText;

                        if (typeName != null)
                        {
                            typeName = Regex.Replace(typeName, @"^[^:]+:", "");
                            var elementDocumentation = xmlDocument
                                .SelectSingleNode(
                                    $"//*[@name='{typeName}']//*[@name='{operationName}']//*[contains(local-name(),'documentation')]")
                                ?.InnerText;
                            if (elementDocumentation != null)
                            {
                                elementDocumentation = ToSummary(elementDocumentation, elementMatch.Groups["space"].Value);
                                classContent = Regex.Replace(classContent,
                                    @$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*public (?<returnType>\S+) {methodName}\b)",
                                    m => elementDocumentation + m.Groups["remainder"].Value,
                                    RegexOptions.Singleline | RegexOptions.Multiline);
                            }
                        }
                    }

                    // enum documentation
                    foreach (Match enumMatch in Regex.Matches(classContent,
                        @"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlEnum(Attribute)?\()(""(?<enumValueName>[^""]+)"")?[^\)]*\)\](?<space>\s+)(?<enumValue>\S+),"))
                    {
                        var enumValue = enumMatch.Groups["enumValueName"].Value;
                        var enumValueName = enumMatch.Groups["enumValue"].Value;
                        //string enumDocumentation = xmlDocument
                        //    .SelectSingleNode(
                        //        $"/*/*[@name='{rootName}']//*[@value='{enumValueName}']//*[contains(local-name(),'documentation')]")
                        //    ?.InnerText;

                        var enumDocumentation = GetRecursiveElement(xmlDocument,
                            $"/*[@name='{rootName}']//*[@value='{enumValueName}']//*[contains(local-name(),'documentation')]", 10);

                        if (enumDocumentation != null)
                        {
                            enumDocumentation = ToSummary(enumDocumentation, enumMatch.Groups["space"].Value);
                            classContent = Regex.Replace(classContent,
                                @$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*{enumValue}\b)",
                                m => enumDocumentation + m.Groups["remainder"].Value,
                                RegexOptions.Singleline | RegexOptions.Multiline);
                        }
                    }

                    fileContent = fileContent.Replace(classMatch.Value, classContent);

                    // complex type documentation
                    //string classDocumentation = xmlDocument
                    //    .SelectSingleNode($"/*/*[@name='{rootName}']//*[contains(name(),'documentation')]")?.InnerText;

                    string classDocumentation = null;

                    if (rootName == "CheckRequestDataType")
                    {

                    }

                    //if (rootName.EndsWith("DataType"))
                    classDocumentation = GetClassDocumentation(xmlDocument, rootName, $"/*[@name='{{name}}']//*[contains(name(),'documentation')]");
                    //else
                    //    classDocumentation = GetRecursiveElement(xmlDocument,
                    //    $"/*[@name='{rootName}']//*[contains(name(),'documentation')]", 10);



                    if (classDocumentation != null)
                    {
                        classDocumentation = ToSummary(classDocumentation, classMatch.Groups["space"].Value);
                        fileContent = Regex.Replace(fileContent,
                            @$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*public (partial class|enum) {className}\b)",
                            m => classDocumentation + m.Groups["remainder"].Value,
                            RegexOptions.Singleline | RegexOptions.Multiline);
                    }
                }
            }

            return fileContent;
        }

        private static string GetElementDocumentation(XmlNode xmlDocument, string rootName, string elementName)
        {
            rootName = TrimWcfEndsStatements(rootName);
            elementName = TrimWcfEndsStatements(elementName);

            rootName = FindClassName(xmlDocument, rootName);

            var qry = $"/*/*/*/*[@name='{rootName}']//*[@name='{elementName}']//*[contains(local-name(),'documentation')]";

            var result = GetRecursiveElement(xmlDocument, qry, 10);

            return result;
        }

        private static string FindClassName(XmlNode xmlDocument, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var orig = name;

            var qry = $"/*[@name='{{name}}']//*[contains(name(),'documentation')]";

            do
            {
                var cName = GetRecursiveElement(xmlDocument, qry.Replace("{name}", name), 10)
                            ?? GetRecursiveElement(xmlDocument, qry.Replace("{name}", name.ToLower()), 10);

                if (cName == null)
                {
                    name = name.Remove(name.Length - 1, 1);
                    while (name.Length > 0 && !char.IsUpper(name[name.Length - 1]))
                    {
                        name = name.Remove(name.Length - 1, 1);
                    }
                    if (name.Length > 0)
                        name = name.Remove(name.Length - 1, 1);
                }
                else
                {
                    return name;
                }
            } while (name.Length > 0);

            name = orig;
            do
            {
                var cName = GetRecursiveElement(xmlDocument, qry.Replace("{name}", name), 10)
                            ?? GetRecursiveElement(xmlDocument, qry.Replace("{name}", name.ToLower()), 10);

                if (cName == null)
                {
                    name = name.Remove(0, 1);
                    while (name.Length > 0 && !char.IsUpper(name[0]))
                    {
                        name = name.Remove(0, 1);
                    }
                }
                else
                {
                    return name;
                }
            } while (name.Length > 0);

            return null;
        }

        private static string GetClassDocumentation(XmlNode xmlDocument, string rootName, string qry)
        {
            rootName = TrimWcfEndsStatements(rootName);

            rootName = FindClassName(xmlDocument, rootName);

            var result = GetRecursiveElement(xmlDocument, qry.Replace("{name}", rootName), 10);

            return result;
        }

        private static string GetRecursiveElement(XmlNode xmlDocument, string qry, int topLevel)
        {
            string res = null;
            for (var i = 0; i < topLevel; i++)
            {
                var node = xmlDocument.SelectSingleNode(qry);
                if (node != null)
                {
                    res = node.InnerText;
                    break;
                }

                qry = "/*" + qry;
            }

            if (res == null)
            {
                //throw new Exception();
            }

            return res;
        }


        public static string RemoveEnd(string src, string entry)
        {
            if (src.StartsWith(entry))
                src = src.Substring(0, src.Length - entry.Length);
            return src;
        }

        /// <summary>
        /// Убирает Wcf окончания
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string TrimWcfEndsStatements(string name)
        {
            var orig = name;
            var els = new[] { "1", "RequestData", "ResponseDataResult", "ResponseData" };
            foreach (var el in els)
            {
                name = RemoveEnd(name, el);
            }

            if (string.IsNullOrEmpty(name))
                return orig;
            return name;
        }

        /// <summary>
        /// Нормализация названий классов
        /// </summary>
        private static string NormalizeClassesName(string name)
        {
            return name.Replace("RequestRequestData", "RequestData")
                .Replace("ResponseResponseDataResult", "ResponseDataResult")
                .Replace("ResponseResponseData", "ResponseData");
        }

        /// <summary>
        /// Возвращает первую не заполненную строку.
        /// </summary>
        private static string GetNotEmptyString(params string[] args)
            => args.FirstOrDefault(s => !string.IsNullOrEmpty(s));
    }
}

