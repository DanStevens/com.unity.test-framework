using System;
using System.IO;
using UnityEditor.TestRunner.CommandLineParser;
using UnityEditor.TestTools.TestRunner.Api;

namespace UnityEditor.TestTools.TestRunner.CommandLineTest
{
    internal class SettingsBuilder : ISettingsBuilder
    {
        private ITestSettingsDeserializer m_TestSettingsDeserializer;
        private Action<string> m_LogAction;
        private Action<string> m_LogWarningAction;
        private Func<string, bool> m_FileExistsCheck;
        private Func<bool> m_ScriptCompilationFailedCheck;
        public SettingsBuilder(ITestSettingsDeserializer testSettingsDeserializer, Action<string> logAction, Action<string> logWarningAction, Func<string, bool> fileExistsCheck, Func<bool> scriptCompilationFailedCheck)
        {
            m_LogAction = logAction;
            m_LogWarningAction = logWarningAction;
            m_FileExistsCheck = fileExistsCheck;
            m_ScriptCompilationFailedCheck = scriptCompilationFailedCheck;
            m_TestSettingsDeserializer = testSettingsDeserializer;
        }

        public Api.ExecutionSettings BuildApiExecutionSettings(string[] commandLineArgs)
        {
            var quit = false;
            string testPlatform = TestMode.EditMode.ToString();
            string[] testFilters = null;
            string[] testCategories = null;
            string testSettingsFilePath = null;

            var optionSet = new CommandLineOptionSet(
                new CommandLineOption("quit", () => { quit = true; }),
                new CommandLineOption("testPlatform", platform => { testPlatform = platform; }),
                new CommandLineOption("editorTestsFilter", filters => { testFilters = filters; }),
                new CommandLineOption("testFilter", filters => { testFilters = filters; }),
                new CommandLineOption("editorTestsCategories", catagories => { testCategories = catagories; }),
                new CommandLineOption("testCategory", catagories => { testCategories = catagories; }),
                new CommandLineOption("testSettingsFile", settingsFilePath => { testSettingsFilePath = settingsFilePath; })
            );
            optionSet.Parse(commandLineArgs);

            DisplayQuitWarningIfQuitIsGiven(quit);

            CheckForScriptCompilationErrors();

            LogParametersForRun(testPlatform, testFilters, testCategories, testSettingsFilePath);

            var testSettings = GetTestSettings(testSettingsFilePath);

            var filter = new Filter()
            {
                groupNames = testFilters,
                categoryNames = testCategories
            };

            var buildTarget = SetFilterAndGetBuildTarget(testPlatform, filter);

            return new Api.ExecutionSettings()
            {
                filter = filter,
                overloadTestRunSettings = new RunSettings(testSettings),
                targetPlatform = buildTarget
            };
        }

        public ExecutionSettings BuildExecutionSettings(string[] commandLineArgs)
        {
            string resultFilePath = null;

            var optionSet = new CommandLineOptionSet(
                new CommandLineOption("editorTestsResultFile", filePath => { resultFilePath = filePath; }),
                new CommandLineOption("testResults", filePath => { resultFilePath = filePath; })
            );
            optionSet.Parse(commandLineArgs);

            var projectPath = Path.GetDirectoryName(resultFilePath);
            return new ExecutionSettings()
            {
                TestResultsFile = resultFilePath,
                ProjectPath = projectPath
            };
        }

        void DisplayQuitWarningIfQuitIsGiven(bool quitIsGiven)
        {
            if (quitIsGiven)
            {
                m_LogWarningAction("Running tests from command line arguments will not work when \"quit\" is specified.");
            }
        }

        void CheckForScriptCompilationErrors()
        {
            if (m_ScriptCompilationFailedCheck())
            {
                throw new SetupException(SetupException.ExceptionType.ScriptCompilationFailed);
            }
        }

        void LogParametersForRun(string testPlatform, string[] testFilters, string[] testCategories, string testSettingsFilePath)
        {
            m_LogAction("Running tests for " + testPlatform);
            if (testFilters != null && testFilters.Length > 0)
            {
                m_LogAction("With test filter: " + string.Join(", ", testFilters));
            }
            if (testCategories != null && testCategories.Length > 0)
            {
                m_LogAction("With test categories: " + string.Join(", ", testCategories));
            }
            if (!string.IsNullOrEmpty(testSettingsFilePath))
            {
                m_LogAction("With test settings file: " + testSettingsFilePath);
            }
        }

        ITestSettings GetTestSettings(string testSettingsFilePath)
        {
            ITestSettings testSettings = null;
            if (!string.IsNullOrEmpty(testSettingsFilePath))
            {
                if (!m_FileExistsCheck(testSettingsFilePath))
                {
                    throw new SetupException(SetupException.ExceptionType.TestSettingsFileNotFound, testSettingsFilePath);
                }

                testSettings = m_TestSettingsDeserializer.GetSettingsFromJsonFile(testSettingsFilePath);
            }
            return testSettings;
        }

        static BuildTarget? SetFilterAndGetBuildTarget(string testPlatform, Filter filter)
        {
            BuildTarget? buildTarget = null;
            if (testPlatform.ToLower() == "editmode")
            {
                filter.testMode = TestMode.EditMode;
            }
            else if (testPlatform.ToLower() == "playmode")
            {
                filter.testMode = TestMode.PlayMode;
            }
            else
            {
                try
                {
                    buildTarget = (BuildTarget)Enum.Parse(typeof(BuildTarget), testPlatform, true);

                    filter.testMode = TestMode.PlayMode;
                }
                catch (ArgumentException)
                {
                    throw new SetupException(SetupException.ExceptionType.PlatformNotFound, testPlatform);
                }
            }
            return buildTarget;
        }
    }
}