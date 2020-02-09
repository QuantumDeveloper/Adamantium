using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Adamantium.Build.Core;
using Adamantium.Core;
using Microsoft.Build.Framework;
using Logger = Adamantium.Core.Logger;

namespace Adamantium.Build.Tasks
{
    public abstract class ContentCompilerTask : CompilerTask
    {
        private static readonly Regex parseMessage = new Regex(@"(.*)\s*\(\s*(\d+)\s*,\s*([^ \)]+)\)\s*:\s*(\w+)\s+(\w+)\s*:\s*(.*)");
        private static readonly Regex matchNumberRange = new Regex(@"(\d+)-(\d+)");

        protected override bool ProcessItem(EngineItem item)
        {
            var hasErrors = false;
            var inputFilePath = item.InputFilePath;

            try
            {
                //var dependencyFilePath = Path.Combine(Path.Combine(ProjectDirectory.ItemSpec, IntermediateDirectory.ItemSpec),
                //                                      FileDependencyList.GetDependencyFileNameFromSourcePath(item.LinkName));
                var dependencyFilePath = Path.Combine(Path.Combine(ProjectDirectory.ItemSpec, IntermediateDirectory.ItemSpec));
                                                      
                Debugger.Launch();
                CreateDirectoryIfNotExists(dependencyFilePath);
                Log.LogMessage(MessageImportance.Low, "Check Engine file to compile {0} with dependency file {1}", inputFilePath, dependencyFilePath);
                if (IsInputFileWasModified(item.InputFilePath, Path.Combine(item.BinaryOutputFilePath, item.OutputLink)) || !File.Exists(item.BinaryOutputFilePath))
                {
                    Log.LogMessage(MessageImportance.Low, "Starting compilation of {0}", inputFilePath);

                    var logger = ProcessFileAndGetLogResults(inputFilePath, dependencyFilePath, item);

                    LogLogger(logger);

                    if (logger.HasErrors)
                    {
                        hasErrors = true;
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.High, "Compilation successful of {0} to {1}", inputFilePath, item.BinaryOutputFilePath);
                    }
                }

            }
            catch (Exception ex)
            {
                Log.LogError("Cannot process file '{0}' : {1}", inputFilePath, ex.Message);
                hasErrors = true;
            }

            return !hasErrors;
        }

        protected abstract Logger ProcessFileAndGetLogResults(string inputFilePath, string dependencyFilePath,
           EngineItem item);


        protected void CreateDirectoryIfNotExists(string filePath)
        {
            var dependencyDirectoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dependencyDirectoryPath))
                Directory.CreateDirectory(dependencyDirectoryPath);
        }

        private void LogLogger(Logger logs)
        {
            if (logs == null)
                return;

            foreach (var message in logs.Messages)
            {
                if (parseLogMessages)
                    LogParsedMessage(message);
                else
                    LogSimpleMessage(message);
            }
        }

        private void LogSimpleMessage(LogMessage message)
        {
            switch (message.Type)
            {
                case LogMessageType.Warning:
                    Log.LogWarning(message.Text);
                    break;
                case LogMessageType.Error:
                    Log.LogError(message.Text);
                    break;
                case LogMessageType.Info:
                    Log.LogMessage(MessageImportance.Low, message.Text);
                    break;
            }
        }

        private void LogParsedMessage(LogMessage message)
        {
            var text = message.ToString();

            string line = null;
            var textReader = new StringReader(text);
            while ((line = textReader.ReadLine()) != null)
            {
                var match = parseMessage.Match(line);
                if (match.Success)
                {
                    var filePath = match.Groups[1].Value;
                    var lineNumber = int.Parse(match.Groups[2].Value);
                    var colNumberText = match.Groups[3].Value;
                    int colStartNumber;
                    int colEndNumber;
                    var colMatch = matchNumberRange.Match(colNumberText);
                    if (colMatch.Success)
                    {
                        int.TryParse(colMatch.Groups[1].Value, out colStartNumber);
                        int.TryParse(colMatch.Groups[2].Value, out colEndNumber);
                    }
                    else
                    {
                        int.TryParse(colNumberText, out colStartNumber);
                        colEndNumber = colStartNumber;
                    }

                    var msgType = match.Groups[4].Value;
                    var msgCode = match.Groups[5].Value;
                    var msgText = match.Groups[6].Value;

                    if (string.Compare(msgType, "error", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        Log.LogError(string.Empty, msgCode, string.Empty, filePath, lineNumber, colStartNumber, lineNumber, colEndNumber, msgText);
                    }
                    else if (string.Compare(msgType, "warning", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        Log.LogWarning(string.Empty, msgCode, string.Empty, filePath, lineNumber, colStartNumber, lineNumber, colEndNumber, msgText);
                    }
                    else if (string.Compare(msgType, "info", StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        Log.LogWarning(string.Empty, msgCode, string.Empty, filePath, lineNumber, colStartNumber, lineNumber, colEndNumber, msgText);
                    }
                    else
                    {
                        Log.LogWarning(line);
                    }
                }
                else
                {
                    Log.LogWarning(line);
                }
            }
        }

        protected bool IsInputFileWasModified(string inputFile, string outputFile)
        {
            var inputModifTime = File.GetLastWriteTime(inputFile).Ticks;
            var outputModifTime = File.GetLastWriteTime(outputFile).Ticks;
            return inputModifTime > outputModifTime;
        }
    }
}
