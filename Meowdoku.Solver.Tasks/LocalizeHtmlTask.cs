using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Meowdoku.Solver.Tasks;

public class LocalizeHtmlTask : Task
{
    /// <summary>
    /// Путь к основному файлу ресурсов (например, Resources.resx).
    /// По нему определяются все связанные .resx с суффиксами культур.
    /// </summary>
    [Required]
    public string ResourceFile { get; set; }

    /// <summary>
    /// Папка, в которой рекурсивно ищутся .html файлы.
    /// </summary>
    [Required]
    public string SourceFolder { get; set; }

    /// <summary>
    /// Корневая папка для сохранения локализованных копий.
    /// Если не указана, используется SourceFolder.
    /// </summary>
    public string? OutputFolder { get; set; }

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.High, "Start HTML localization");
        // SpinWait.SpinUntil(() => Debugger.IsAttached);
        try
        {
            // 1. Определяем папку ресурсов и базовое имя
            var resourceDir = Path.GetDirectoryName(ResourceFile) ?? ".";
            Log.LogMessage(MessageImportance.High, "Resource directory: '{0}'", resourceDir);

            var baseName = Path.GetFileNameWithoutExtension(ResourceFile);
            Log.LogMessage(MessageImportance.High, "Base resource file: '{0}'", baseName);

            if (string.IsNullOrEmpty(baseName))
            {
                Log.LogError("Error while detect base resource file name from '{0}'", ResourceFile);
                return false;
            }

            // 2. Находим все .resx, начинающиеся с baseName
            var resxFiles = Directory.GetFiles(resourceDir, baseName + "*.resx");

            if (resxFiles.Length == 0)
            {
                Log.LogError("Не найдено ни одного файла ресурсов с базовым именем '{0}' в папке '{1}'", baseName,
                    resourceDir);
                return false;
            }

            Log.LogMessage(MessageImportance.High, "Found {0} resource files: {1}", resxFiles.Length,
                string.Join(", ", resxFiles.Order()));

            // 3. Загружаем ресурсы по культурам
            var cultureResources = new Dictionary<string, Dictionary<string, string>>();

            foreach (var file in resxFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var culture = "";
                if (fileName.Length > baseName.Length + 1 && fileName.StartsWith(baseName + "."))
                {
                    culture = fileName.Substring(baseName.Length + 1);
                }

                if (cultureResources.ContainsKey(culture))
                    continue; // если несколько файлов с одной культурой, берём первый

                var dict = LoadResx(file);
                cultureResources.Add(culture, dict);
                Log.LogMessage(MessageImportance.High, "Загружено {0} ресурсов для культуры '{1}' из {2}", dict.Count,
                    culture, file);
            }

            if (cultureResources.Count == 0)
            {
                Log.LogError("Cultures not found");
                return false;
            }

            Log.LogMessage(MessageImportance.High, "Found {0} cultures: {1}", cultureResources.Count,
                string.Join(", ", cultureResources.Keys.Select(x => $"'{x}'")));

            // 4. Определяем выходную папку
            var outputRoot = string.IsNullOrEmpty(OutputFolder) ? SourceFolder : OutputFolder;

            Log.LogMessage(MessageImportance.High, "Output folder: '{0}'", outputRoot);

            // 5. Ищем все .html в SourceFolder
            var htmlFiles = Directory.GetFiles(SourceFolder, "*.html", SearchOption.AllDirectories);

            if (htmlFiles.Length == 0)
            {
                Log.LogWarning("HTML files not found in '{0}'", SourceFolder);
                return true;
            }

            Log.LogMessage(MessageImportance.High, "Found {0} html files:\n{1}", htmlFiles.Length,
                string.Join(Environment.NewLine, htmlFiles.Order()));

            // 6. Обрабатываем каждый HTML для каждой культуры
            foreach (var htmlFile in htmlFiles)
            {
                var content = File.ReadAllText(htmlFile);
                var relativePath = Path.GetRelativePath(SourceFolder, htmlFile);
                var fileNameOnly = Path.GetFileName(htmlFile);

                foreach (var (culture, resources) in cultureResources)
                {
                    Log.LogMessage(MessageImportance.High, "Localizing HTML file '{0}' with '{1}' culture", htmlFile,
                        culture);

                    var localizedContent = content;

                    var cultureNamePlaceholder = "{{culture-name-placeholder}}";
                    Log.LogMessage(MessageImportance.High, "Replace culture name placeholder to '{0}'...", culture);
                    localizedContent = localizedContent.Replace(cultureNamePlaceholder, culture);

                    var cultureDisplayNamePlaceholder = "{{culture-display-name-placeholder}}";

                    var allCulturesJsonDictPlaceholder = "{{all-cultures-dictionary-placeholder}}";

                    var allCulturesDisplayNameByName = cultureResources.Keys
                        .Select(x =>
                        {
                            var cultureDisplayName = new CultureInfo(x).DisplayName;
                            cultureDisplayName =
                                $"{cultureDisplayName[0].ToString().ToUpperInvariant()}{cultureDisplayName[1..]}";
                            return new KeyValuePair<string, string>(x, string.IsNullOrWhiteSpace(x)
                                ? "English"
                                : cultureDisplayName);
                        })
                        .ToDictionary();

                    var allCulturesJson = JsonSerializer.Serialize(
                        allCulturesDisplayNameByName
                    );

                    localizedContent = localizedContent.Replace(allCulturesJsonDictPlaceholder, allCulturesJson);


                    var cultureDisplayName = string.IsNullOrWhiteSpace(culture)
                        ? "English"
                        : new CultureInfo(culture).DisplayName;

                    cultureDisplayName =
                        $"{cultureDisplayName[0].ToString().ToUpperInvariant()}{cultureDisplayName[1..]}";

                    Log.LogMessage(MessageImportance.High, "Replace culture display name placeholder to '{0}'...",
                        cultureDisplayName);

                    localizedContent = localizedContent.Replace(cultureDisplayNamePlaceholder, cultureDisplayName);

                    foreach (var res in resources)
                    {
                        var placeholder = "{{" + res.Key + "}}";
                        Log.LogMessage(MessageImportance.High, "Replace data for key '{0}'...", placeholder);
                        localizedContent = localizedContent.Replace(placeholder, res.Value);
                    }

                    // var cultureFolder = string.IsNullOrEmpty(culture) ? "neutral" : culture;
                    var targetFolder = Path.Combine(outputRoot, culture);
                    var targetSubDir = Path.GetDirectoryName(relativePath);
                    var fullTargetDir = Path.Combine(targetFolder, targetSubDir ?? "");
                    Directory.CreateDirectory(fullTargetDir);

                    var targetFile = Path.Combine(fullTargetDir, fileNameOnly);
                    File.WriteAllText(targetFile, localizedContent);

                    Log.LogMessage(MessageImportance.High, "Write {0} for '{1}'", targetFile, culture);
                }
            }

            Log.LogMessage(MessageImportance.High, "All HTML files are localized.");
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex);
            return false;
        }
    }

    /// <summary>
    /// Загружает ресурсы из .resx файла (формат XML).
    /// </summary>
    private static Dictionary<string, string> LoadResx(string resxPath)
    {
        var dict = new Dictionary<string, string>();
        var doc = XDocument.Load(resxPath);
        var dataElements = doc.Root?.Elements("data");
        if (dataElements == null)
            return dict;

        foreach (var data in dataElements)
        {
            var key = data.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(key))
                continue;

            var valueElement = data.Element("value");
            var value = valueElement?.Value ?? "";
            dict[key] = value;
        }

        return dict;
    }
}