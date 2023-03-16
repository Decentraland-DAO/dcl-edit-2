#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Scripts.EditorState;
using Assets.Scripts.SceneState;
using Assets.Scripts.Utility;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zenject;
using static Assets.Scripts.System.SettingsSystem;

namespace Assets.Scripts.System
{
    public class CustomComponentMarkupSystem
    {
        public class CustomComponentProblem : IFileReadingProblem
        {
            public CustomComponentProblem(string description, string location)
            {
                this.description = description;
                this.location = location;
            }

            public string description { get; }
            public string location { get; }
        }

        public class CustomComponentException : Exception, IFileReadingProblem
        {
            public CustomComponentException(string description, int line, int column, string path /*, string lineText*/)
            {
                this.description = description;
                this.line = line;
                this.column = column;
                this.path = path;
                //this.lineText = lineText;
            }

            public CustomComponentException(string description, JToken token, string path)
            {
                if (token is IJsonLineInfo lineInfo)
                {
                    lineInfo.HasLineInfo();

                    this.line = lineInfo.LineNumber;
                    this.column = lineInfo.LinePosition;
                }

                this.description = description;
                this.path = path;
            }

            public string description { get; }
            public string path { get; }

            private readonly int line = -1;
            private readonly int column = -1;
            //private readonly string lineText;

            public override string Message =>
                $"{description}\n  at {location}";


            public string location => $"{path}:{line}:{column}";

            //public string faultyCode => $"{lineText}\n{"^".Indent(column, " ")}";
        }

        // Dependencies
        private FileManagerSystem fileManagerSystem;
        private AvailableComponentsState availableComponentsState;
        private IPathState pathState;

        [Inject]
        public void Construct(
            FileManagerSystem fileManagerSystem,
            AvailableComponentsState availableComponentsState,
            IPathState pathState)
        {
            this.fileManagerSystem = fileManagerSystem;
            this.availableComponentsState = availableComponentsState;
            this.pathState = pathState;
        }

        private const string sourcePathKey = "shiylheavarojzbhqacgzybfge"; // magic string

        private const string classKey = "class";
        private const string componentKey = "component";
        private const string importFileKey = "import-file";
        private const string propertiesKey = "properties";

        private const string fileExtensionTs = ".ts";
        private const string fileExtensionJs = ".js";
        private const string fileExtensionCompFile = ".dcecomp";

        public const string exceptionMessageClassNotPresent = "A component has to have a class, that contains the name of the component class";
        public const string exceptionMessageClassWrongType = "Class has to be a string, that contains the class name of the component";
        public const string exceptionMessageComponentWrongType = "Component has to be a string, that contains the component name";
        public const string exceptionMessageImportFileNotPresent = "A component in a .dcecomp file has to have a import-file property, that contains the path to the file to import the component from relative to the project root";
        public const string exceptionMessageImportFileWrongType = "Import file has to be a string, that contains the path to the file to import the component from relative to the project root";
        public const string exceptionMessagePropertyNameNotPresent = "The property has to have a name";
        public const string exceptionMessagePropertyNameWrongType = "The property name has to be a string";
        public const string exceptionMessagePropertyTypeNotPresent = "The property has to have a type";
        public const string exceptionMessagePropertyTypeWrongType = "The property type has to be a string";
        public const string exceptionMessagePropertyTypeWrongOption = "Type has to be one of the following values: \"string\", \"number\", \"vector3\"";
        public const string exceptionMessageDefaultWrongTypeString = "The default of a property of type string has to be a string";
        public const string exceptionMessageDefaultWrongTypeInt = "The default of a property of type int has to be an number";
        public const string exceptionMessageDefaultWrongTypeNumber = "The default of a property of type number has to be a number";
        public const string exceptionMessageDefaultWrongTypeBool = "The default of a property of type bool has to be a bool";
        public const string exceptionMessageInvalidJson = "A #DCECOMP tag was not followed by valid Json";


        public List<IFileReadingProblem> SetupCustomComponents()
        {
            try
            {
                var customComponentProblems = new List<IFileReadingProblem>();

                var markUpDates =
                    fileManagerSystem
                        .GetAllFilesWithExtension(fileExtensionJs, fileExtensionTs, fileExtensionCompFile)
                        .SelectMany(path => FindCustomComponentMarkups(path, customComponentProblems));

                foreach (var componentObject in markUpDates)
                {
                    var path = componentObject[sourcePathKey]!.Value<string>();

                    try
                    {
                        MakeComponent(componentObject, path);
                    }
                    catch (CustomComponentException cce)
                    {
                        customComponentProblems.Add(cce);
                    }
                }

                foreach (var problem in customComponentProblems)
                {
                    Debug.LogWarning($"{problem.description} at {problem.location}");
                }

                return customComponentProblems;
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                return new List<IFileReadingProblem>
                {
                    new CustomComponentProblem(e.Message, "")
                };
            }
        }

        private void MakeComponent(JObject componentData, string path)
        {
            // Component Class
            // Get class
            var classToken = componentData[classKey] ?? throw new CustomComponentException(exceptionMessageClassNotPresent, componentData, path);

            // Interpret class
            var classValue = classToken.Value<string?>() ?? throw new CustomComponentException(exceptionMessageClassWrongType, classToken, path);


            // Component Name
            // Get component name
            var componentToken = componentData[componentKey];

            // Interpret component name
            string? componentValue = null;
            if (componentToken != null)
            {
                componentValue = componentToken.Value<string?>() ?? throw new CustomComponentException(exceptionMessageComponentWrongType, componentToken, path);
            }


            // Import File
            // Get import file
            var importFileToken = componentData[importFileKey] ?? throw new CustomComponentException(exceptionMessageImportFileNotPresent, componentData, path);

            // Interpret import file
            var importFileValue = importFileToken.Value<string?>() ?? throw new CustomComponentException(exceptionMessageImportFileWrongType, importFileToken, path);


            // Properties
            // Get properties
            var propertiesToken = componentData[propertiesKey];

            // Get definitions
            var propertyDefinitions = GetPropertyDefinitions(propertiesToken, path);


            // Setup
            // Setup component definition
            var componentDefinition = new DclComponent.ComponentDefinition(
                classValue,
                componentValue ?? classValue,
                importFileValue,
                propertyDefinitions);

            // Setup available component
            var availableComponent = new AvailableComponentsState.AvailableComponent
            {
                category = "Custom",
                availableInAddComponentMenu = true,
                componentDefinition = componentDefinition
            };

            // Push available component (set or overwrite)
            availableComponentsState.UpdateCustomComponent(availableComponent);
        }

        private DclComponent.DclComponentProperty.PropertyDefinition[] GetPropertyDefinitions(JToken? propertiesToken, string path)
        {
            return propertiesToken?
                .Select(p =>
                {
                    // Get name
                    var nameToken = p["name"] ?? throw new CustomComponentException(exceptionMessagePropertyNameNotPresent, p, path);

                    // Interpret name
                    var nameValue = nameToken.Value<string?>() ?? throw new CustomComponentException(exceptionMessagePropertyNameWrongType, nameToken, path);


                    // Get type
                    var type = p["type"] ?? throw new CustomComponentException(exceptionMessagePropertyTypeNotPresent, p, path);

                    // Interpret type
                    var typeValue = GetTypeFromString(type, path);


                    // Get default
                    var defaultToken = p["default"];

                    // Interpret type
                    var defaultValue = GetDefaultPropertyValue(typeValue, defaultToken, path);

                    return new DclComponent.DclComponentProperty.PropertyDefinition(
                        nameValue,
                        typeValue,
                        defaultValue);
                })
                .ToArray() ?? Array.Empty<DclComponent.DclComponentProperty.PropertyDefinition>(); // if properties is not present, create an empty property definition list
        }

        private DclComponent.DclComponentProperty.PropertyType GetTypeFromString(JToken typeToken, string path)
        {
            var typeString = typeToken.Value<string?>() ?? throw new CustomComponentException(exceptionMessagePropertyTypeWrongType, typeToken, path);

            return typeString switch
            {
                "string" => DclComponent.DclComponentProperty.PropertyType.String,
                "number" => DclComponent.DclComponentProperty.PropertyType.Float,
                "vector3" => DclComponent.DclComponentProperty.PropertyType.Vector3,
                _ => throw new CustomComponentException(exceptionMessagePropertyTypeWrongOption, typeToken, "")
            };
        }

        private dynamic? GetDefaultPropertyValue(DclComponent.DclComponentProperty.PropertyType type, JToken? value, string path)
        {
            if (value == null)
            {
                return GetDefaultDefaultFromType(type);
            }

            return type switch
            {
                DclComponent.DclComponentProperty.PropertyType.None => null,
                DclComponent.DclComponentProperty.PropertyType.String => GetStringFromJToken(value, path),
                DclComponent.DclComponentProperty.PropertyType.Int => GetIntFromJToken(value, path),
                DclComponent.DclComponentProperty.PropertyType.Float => GetFloatFromJObject(value, path),
                DclComponent.DclComponentProperty.PropertyType.Boolean => GetBoolFromJObject(value, path),
                DclComponent.DclComponentProperty.PropertyType.Vector3 => GetVector3FromJObject(value, path),
                DclComponent.DclComponentProperty.PropertyType.Quaternion => GetQuaternionFromJObject(value, path),
                DclComponent.DclComponentProperty.PropertyType.Asset => GetGuidFromJObject(value, path),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        private string GetStringFromJToken(JToken token, string path)
        {
            return token.Value<string?>() ?? throw new CustomComponentException(exceptionMessageDefaultWrongTypeString, token, path);
        }

        private int GetIntFromJToken(JToken token, string path)
        {
            return token.Value<int?>() ?? throw new CustomComponentException(exceptionMessageDefaultWrongTypeInt, token, path);
        }

        private float GetFloatFromJObject(JToken token, string path)
        {
            return token.Value<float?>() ?? throw new CustomComponentException(exceptionMessageDefaultWrongTypeNumber, token, path);
        }

        private bool GetBoolFromJObject(JToken token, string path)
        {
            return token.Value<bool?>() ?? throw new CustomComponentException(exceptionMessageDefaultWrongTypeBool, token, path);
        }

        private Vector3 GetVector3FromJObject(JToken token, string path)
        {
            // value is a list with 3 numbers
            // extract that list
            var list = token.Value<JArray>();

            // TODO: catch errors

            // extract the numbers
            var x = list[0].Value<float>();
            var y = list[1].Value<float>();
            var z = list[2].Value<float>();

            // return the vector
            return new Vector3(x, y, z);
        }

        private Quaternion GetQuaternionFromJObject(JToken token, string path)
        {
            return Quaternion.identity; // TODO: get actual default value
        }

        private Guid GetGuidFromJObject(JToken token, string path)
        {
            return Guid.Empty;
        }


        private dynamic? GetDefaultDefaultFromType(DclComponent.DclComponentProperty.PropertyType type)
        {
            return type switch
            {
                DclComponent.DclComponentProperty.PropertyType.None => null,
                DclComponent.DclComponentProperty.PropertyType.String => "",
                DclComponent.DclComponentProperty.PropertyType.Int => 0,
                DclComponent.DclComponentProperty.PropertyType.Float => 0f,
                DclComponent.DclComponentProperty.PropertyType.Boolean => false,
                DclComponent.DclComponentProperty.PropertyType.Vector3 => Vector3.zero,
                DclComponent.DclComponentProperty.PropertyType.Quaternion => Quaternion.identity,
                DclComponent.DclComponentProperty.PropertyType.Asset => Guid.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public JObject[] FindCustomComponentMarkups(string path, List<IFileReadingProblem> problems)
        {
            try
            {
                var fileContents = File.ReadAllText(path);

                // if file is js or ts, filter out everything, that is not a comment
                var isJsOrTs = Path.GetExtension(path) == fileExtensionJs || Path.GetExtension(path) == fileExtensionTs;
                if (isJsOrTs)
                {
                    fileContents = FilterComments(fileContents);
                }

                // find raw component markup texts
                var indices = GetIndicesOf(fileContents, "#DCECOMP");

                // limit markup texts to the json only
                var jsonTexts = indices.Select(i => ExtractJsonFromRawString(fileContents, i + 8));

                var enumerable = jsonTexts as string[] ?? jsonTexts.ToArray();
                if (enumerable.Length == 0)
                    return Array.Empty<JObject>();

                //Debug.Log($"jsonTexts.First(): {enumerable.First()}");


                return enumerable
                    .SelectMany(t =>
                    {
                        try
                        {
                            var jObject = JObject.Load(new JsonTextReader(new StringReader(t)));
                            jObject[sourcePathKey] = path;
                            return jObject.InEnumerable();
                        }
                        catch (Exception)
                        {
                            problems.Add(new CustomComponentProblem(exceptionMessageInvalidJson, path));
                            return Enumerable.Empty<JObject>();
                        }
                    })
                    .Select(d =>
                    {
                        if (isJsOrTs && d[importFileKey] is null)
                        {
                            d[importFileKey] = GetImportPathFromFilePath(path);
                        }

                        return d;
                    })
                    .ToArray();
            }
            catch (Exception e)
            {
                problems.Add(new CustomComponentProblem(e.Message, path));

                return Array.Empty<JObject>();
            }
        }

        private List<int> GetIndicesOf(string haystack, string needle)
        {
            var i = 0;
            var result = new List<int>();

            while (i >= 0)
            {
                var newFind = haystack.IndexOf(needle, i, StringComparison.InvariantCulture);

                if (newFind >= 0)
                {
                    result.Add(newFind);
                    i = newFind + 1;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private string GetImportPathFromFilePath(string path)
        {
            var relativePath = StaticUtilities.MakeRelativePath(pathState.ProjectPath, path);
            var folderPath = Path.GetDirectoryName(relativePath);
            var fileWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
            var folderWithFile = Path.Combine(folderPath, fileWithoutExtension);
            var pathWithForwardSlashes = folderWithFile.Replace('\\', '/');
            return pathWithForwardSlashes;
        }

        private string ExtractJsonFromRawString(string value, int startingFrom)
        {
            var reader = new StringReader(value);
            var result = new StringBuilder();
            var bracketLevel = 0;

            // go to start
            for (int i = 0; i < startingFrom; i++)
            {
                var c = (char) reader.Read();

                if (c == '\r')
                {
                    result.Append("\r");
                }
                else if (c == '\n')
                {
                    result.Append("\n");
                }
                else
                {
                    result.Append(" ");
                }
            }

            // find start
            while (reader.Peek() >= 0)
            {
                var c = (char) reader.Read();

                if (c == '{')
                {
                    result.Append('{');
                    bracketLevel++;
                    break;
                }

                switch (c)
                {
                    case '\r':
                        result.Append("\r");
                        break;
                    case '\n':
                        result.Append("\n");
                        break;
                    default:
                        result.Append(" ");
                        break;
                }
            }

            // extract json
            while (reader.Peek() >= 0)
            {
                var c = (char) reader.Read();

                switch (c)
                {
                    case '{':
                        bracketLevel++;
                        break;
                    case '}':
                        bracketLevel--;
                        break;
                }

                if (bracketLevel <= 0)
                {
                    result.Append(c);
                    break;
                }

                result.Append(c);
            }

            return result.ToString();
        }

        /// <summary>
        /// Changes text so that only comments remain
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string FilterComments(string text)
        {
            // 0: no comment
            // 1: block comment
            // 2: line comment
            var commentMode = 0;
            var result = new StringBuilder();

            for (var i = 0; i < text.Length; i++)
            {
                if (commentMode == 0)
                {
                    // if currently not in a comment
                    // see if the next text is "/*"
                    if (text[i] == '/' && text[i + 1] == '*')
                    {
                        // start a block comment
                        commentMode = 1;
                        i++;
                        result.Append("  ");
                    }
                    else if (text[i] == '/' && text[i + 1] == '/')
                    {
                        // start a line comment
                        commentMode = 2;
                        i++;
                        result.Append("  ");
                    }
                    else
                    {
                        if (text[i] == '\n')
                        {
                            result.Append("\n");
                        }
                        else if (text[i] == '\r')
                        {
                            result.Append("\r");
                        }
                        else
                        {
                            result.Append(" ");
                        }
                    }
                }
                else if (commentMode == 1)
                {
                    // if currently in a block comment
                    // see if the next text is */
                    if (text[i] == '*' && text[i + 1] == '/')
                    {
                        // end the block comment
                        commentMode = 0;
                        i++;
                        result.Append("  ");
                    }
                    else
                    {
                        // add the current character to the result
                        result.Append(text[i]);
                    }
                }
                else if (commentMode == 2)
                {
                    // if currently in a line comment
                    // see if the next text is a new line
                    if (text[i] == '\n')
                    {
                        // end the line comment
                        result.Append("\n");
                        commentMode = 0;
                    }
                    else if (text[i] == '\r')
                    {
                        // end the line comment
                        result.Append("\r");
                        commentMode = 0;
                    }
                    else
                    {
                        // add the current character to the result
                        result.Append(text[i]);
                    }
                }
            }

            return result.ToString();
        }
    }
}
