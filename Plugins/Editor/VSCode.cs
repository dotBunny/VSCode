/*
 * Unity VSCode Support
 *
 * Seamless support for Microsoft Visual Studio Code in Unity
 *
 * Version:
 *   2.45
 *
 * Authors:
 *   Matthew Davey <matthew.davey@dotbunny.com>
 */
// REQUIRES: VSCode 0.8.0 - Settings directory moved to .vscode
// TODO: Currently VSCode will not debug mono on Windows -- need a solution.
namespace dotBunny.Unity
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class VSCode
    {
        /// <summary>
        /// Current Version Number
        /// </summary>
        public const float Version = 2.45f;

        /// <summary>
        /// Current Version Code
        /// </summary>
        public const string VersionCode = "-RELEASE";
        
        /// <summary>
        /// Download URL for Unity Debbuger
        /// </summary>
        public const string UnityDebuggerURL = "https://raw.githubusercontent.com/dotBunny/VSCode-Test/master/Downloads/unity-debug-101.vsix";

        #region Properties

        /// <summary>
        /// Should debug information be displayed in the Unity terminal?
        /// </summary>
        public static bool Debug
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_Debug", false);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_Debug", value);
            }
        }

        /// <summary>
        /// Is the Visual Studio Code Integration Enabled?
        /// </summary>
        /// <remarks>
        /// We do not want to automatically turn it on, for in larger projects not everyone is using VSCode
        /// </remarks>
        public static bool Enabled
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_Enabled", false);
            }
            set
            {
                // When turning the plugin on, we should remove all the previous project files
                if (!Enabled && value)
                {
                    ClearProjectFiles();
                }
                EditorPrefs.SetBool("VSCode_Enabled", value);
            }
        }
        public static bool UseUnityDebugger
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_UseUnityDebugger", false);
            }
            set
            {
                if ( value != UseUnityDebugger ) {
                    
                    // Set value
                    EditorPrefs.SetBool("VSCode_UseUnityDebugger", value);
                    
                    // Do not write the launch JSON file because the debugger uses its own
                    if ( value ) {
                        WriteLaunchFile = false;
                    }
                    
                    // Update launch file
                    UpdateLaunchFile();
                }
            }
        }

        /// <summary>
        /// Should the launch.json file be written?
        /// </summary>
        /// <remarks>
        /// Useful to disable if someone has their own custom one rigged up
        /// </remarks>
        public static bool WriteLaunchFile
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_WriteLaunchFile", true);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_WriteLaunchFile", value);
            }
        }

        /// <summary>
        /// Should the plugin automatically update itself.
        /// </summary>
        static bool AutomaticUpdates
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_AutomaticUpdates", false);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_AutomaticUpdates", value);
            }
        }

        static float GitHubVersion
        {
            get
            {
                return EditorPrefs.GetFloat("VSCode_GitHubVersion", Version);
            }
            set
            {
                EditorPrefs.SetFloat("VSCode_GitHubVersion", value);
            }
        }

        /// <summary>
        /// When was the last time that the plugin was updated?
        /// </summary>
        static DateTime LastUpdate
        {
            get
            {
                // Feature creation date.
                DateTime lastTime = new DateTime(2015, 10, 8);

                if (EditorPrefs.HasKey("VSCode_LastUpdate"))
                {
                    DateTime.TryParse(EditorPrefs.GetString("VSCode_LastUpdate"), out lastTime);
                }
                return lastTime;
            }
            set
            {
                EditorPrefs.SetString("VSCode_LastUpdate", value.ToString());
            }
        }

        /// <summary>
        /// Quick reference to the VSCode launch settings file
        /// </summary>
        static string LaunchPath
        {
            get
            {
                return SettingsFolder + System.IO.Path.DirectorySeparatorChar + "launch.json";
            }
        }

        /// <summary>
        /// The full path to the project
        /// </summary>
        static string ProjectPath
        {
            get
            {
                return System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
            }
        }

        /// <summary>
        /// Should the script editor be reverted when quiting Unity.
        /// </summary>
        /// <remarks>
        /// Useful for environments where you do not use VSCode for everything.
        /// </remarks>
        static bool RevertExternalScriptEditorOnExit
        {
            get
            {
                return EditorPrefs.GetBool("VSCode_RevertScriptEditorOnExit", true);
            }
            set
            {
                EditorPrefs.SetBool("VSCode_RevertScriptEditorOnExit", value);
            }
        }

        /// <summary>
        /// Quick reference to the VSCode settings folder
        /// </summary>
        static string SettingsFolder
        {
            get
            {
                return ProjectPath + System.IO.Path.DirectorySeparatorChar + ".vscode";
            }
        }

        static string SettingsPath
        {

            get
            {
                return SettingsFolder + System.IO.Path.DirectorySeparatorChar + "settings.json";
            }
        }

        static int UpdateTime
        {
            get
            {
                return EditorPrefs.GetInt("VSCode_UpdateTime", 7);
            }
            set
            {
                EditorPrefs.SetInt("VSCode_UpdateTime", value);
            }
        }

        #endregion

        /// <summary>
        /// Integration Constructor
        /// </summary>
        static VSCode()
        {
            if (Enabled)
            {
                UpdateUnityPreferences(true);
                UpdateLaunchFile();
                
                // Add Update Check
                DateTime targetDate = LastUpdate.AddDays(UpdateTime);
                if (DateTime.Now >= targetDate && AutomaticUpdates)
                {
                    CheckForUpdate();
                }
            }
            
            // Event for when script is reloaded 
            System.AppDomain.CurrentDomain.DomainUnload += System_AppDomain_CurrentDomain_DomainUnload;
        }
        static void System_AppDomain_CurrentDomain_DomainUnload(object sender, System.EventArgs e)
        {
            if (Enabled && RevertExternalScriptEditorOnExit)
            {
                UpdateUnityPreferences(false);
            }
        }


        #region Public Members

        /// <summary>
        /// Force Unity To Write Project File
        /// </summary>
        /// <remarks>
        /// Reflection!
        /// </remarks>
        public static void SyncSolution()
        {
            System.Type T = System.Type.GetType("UnityEditor.SyncVS,UnityEditor");
            System.Reflection.MethodInfo SyncSolution = T.GetMethod("SyncSolution", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            SyncSolution.Invoke(null, null);

        }

        /// <summary>
        /// Update the solution files so that they work with VS Code
        /// </summary>
        public static void UpdateSolution()
        {
            // No need to process if we are not enabled
            if (!VSCode.Enabled)
            {
                return;
            }

            if (VSCode.Debug)
            {
                UnityEngine.Debug.Log("[VSCode] Updating Solution & Project Files");
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionFiles = Directory.GetFiles(currentDirectory, "*.sln");
            var projectFiles = Directory.GetFiles(currentDirectory, "*.csproj");

            foreach (var filePath in solutionFiles)
            {
                string content = File.ReadAllText(filePath);
                content = ScrubSolutionContent(content);

                File.WriteAllText(filePath, content);

                ScrubFile(filePath);
            }

            foreach (var filePath in projectFiles)
            {
                string content = File.ReadAllText(filePath);
                content = ScrubProjectContent(content);

                File.WriteAllText(filePath, content);

                ScrubFile(filePath);
            }

        }

        #endregion

        #region Private Members

        /// <summary>
        /// Call VSCode with arguements
        /// </summary>
        static void CallVSCode(string args)
        {
            System.Diagnostics.Process proc = new System.Diagnostics.Process();

#if UNITY_EDITOR_OSX
            proc.StartInfo.FileName = "open";
            proc.StartInfo.Arguments = " -n -b \"com.microsoft.VSCode\" --args " + args;
            proc.StartInfo.UseShellExecute = false;
#elif UNITY_EDITOR_WIN
            proc.StartInfo.FileName = "code.cmd";
            proc.StartInfo.Arguments = args;
            proc.StartInfo.UseShellExecute = false;
#else
            //TODO: Allow for manual path to code?
            proc.StartInfo.FileName = "code";
            proc.StartInfo.Arguments = args;
            proc.StartInfo.UseShellExecute = false;
#endif
            proc.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            try {
                proc.Start();
            } catch (System.ComponentModel.Win32Exception e){
                UnityEngine.Debug.LogException(e);
                UnityEngine.Debug.LogError("NativeErrorCode: " + e.NativeErrorCode.ToString() +
                    " see https://msdn.microsoft.com/en-us/library/windows/desktop/ms681381%28v=vs.85%29.aspx for error code reference");
            }
        }

        /// <summary>
        /// Check for Updates with GitHub
        /// </summary>
        static void CheckForUpdate()
        {
            var fileContent = string.Empty;

            EditorUtility.DisplayProgressBar("VSCode", "Checking for updates ...", 0.5f);

            // Because were not a runtime framework, lets just use the simplest way of doing this
            try
            {
                using (var webClient = new System.Net.WebClient())
                {
                    fileContent = webClient.DownloadString("https://raw.githubusercontent.com/dotBunny/VSCode/master/Plugins/Editor/VSCode.cs");
                }
            }
            catch (Exception e)
            {
                if (Debug)
                {
                    UnityEngine.Debug.Log("[VSCode] " + e.Message);

                }

                // Don't go any further if there is an error
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Set the last update time
            LastUpdate = DateTime.Now;

            // Fix for oddity in downlo
            if (fileContent.Substring(0, 2) != "/*")
            {
                int startPosition = fileContent.IndexOf("/*", StringComparison.CurrentCultureIgnoreCase);

                // Jump over junk characters
                fileContent = fileContent.Substring(startPosition);
            }

            string[] fileExploded = fileContent.Split('\n');
            if (fileExploded.Length > 7)
            {
                float github = Version;
                if (float.TryParse(fileExploded[6].Replace("*", "").Trim(), out github))
                {
                    GitHubVersion = github;
                }


                if (github > Version)
                {
                    var GUIDs = AssetDatabase.FindAssets("t:Script VSCode");
                    var path = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length) + System.IO.Path.DirectorySeparatorChar +
                               AssetDatabase.GUIDToAssetPath(GUIDs[0]).Replace('/', System.IO.Path.DirectorySeparatorChar);

                    if (EditorUtility.DisplayDialog("VSCode Update", "A newer version of the VSCode plugin is available, would you like to update your version?", "Yes", "No"))
                    {
                        // Always make sure the file is writable
                        System.IO.FileInfo fileInfo = new System.IO.FileInfo(path);
                        fileInfo.IsReadOnly = false;

                        // Write update file
                        File.WriteAllText(path, fileContent);

                        // Force update on text file
                        AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(GUIDs[0]), ImportAssetOptions.ForceUpdate);
                    }

                }
            }
        }

        /// <summary>
        /// Clear out any existing project files and lingering stuff that might cause problems
        /// </summary>
        static void ClearProjectFiles()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionFiles = Directory.GetFiles(currentDirectory, "*.sln");
            var projectFiles = Directory.GetFiles(currentDirectory, "*.csproj");
            var unityProjectFiles = Directory.GetFiles(currentDirectory, "*.unityproj");

            foreach (string solutionFile in solutionFiles)
            {
                File.Delete(solutionFile);
            }
            foreach (string projectFile in projectFiles)
            {
                File.Delete(projectFile);
            }
            foreach (string unityProjectFile in unityProjectFiles)
            {
                File.Delete(unityProjectFile);
            }

            // Replace with our clean files (only in Unity 5)
#if !UNITY_4_0 && !UNITY_4_1 && !UNITY_4_2 && !UNITY_4_3 && !UNITY_4_5 && !UNITY_4_6 && !UNITY_4_7
            SyncSolution();
#endif
        }

        /// <summary>
        /// Force Unity Preferences Window To Read From Settings
        /// </summary>
        static void FixUnityPreferences()
        {
            // I want that window, please and thank you
            System.Type T = System.Type.GetType("UnityEditor.PreferencesWindow,UnityEditor");

            if (EditorWindow.focusedWindow == null)
                return;

            // Only run this when the editor window is visible (cause its what screwed us up)
            if (EditorWindow.focusedWindow.GetType() == T)
            {
                var window = EditorWindow.GetWindow(T, true, "Unity Preferences");


                if (window == null)
                {
                    if (Debug)
                    {
                        UnityEngine.Debug.Log("[VSCode] No Preferences Window Found (really?)");
                    }
                    return;
                }

                var invokerType = window.GetType();
                var invokerMethod = invokerType.GetMethod("ReadPreferences",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (invokerMethod != null)
                {
                    invokerMethod.Invoke(window, null);
                }
                else if (Debug)
                {
                    UnityEngine.Debug.Log("[VSCode] No Reflection Method Found For Preferences");
                }
            }

            //  // Get internal integration class
            //  System.Type iT = System.Type.GetType("UnityEditor.VisualStudioIntegration.UnityVSSupport,UnityEditor.VisualStudioIntegration");
            //  var iinvokerMethod = iT.GetMethod("ScriptEditorChanged", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            //  var temp = EditorPrefs.GetString("kScriptsDefaultApp");
            //  iinvokerMethod.Invoke(null,new object[] { temp } );

        }

        /// <summary>
        /// Determine what port Unity is listening for on Windows
        /// </summary>
        static int GetDebugPort()
        {
#if UNITY_EDITOR_WIN
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "netstat";
            process.StartInfo.Arguments = "-a -n -o -p TCP";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string[] lines = output.Split('\n');

            process.WaitForExit();

            foreach (string line in lines)
            {
                string[] tokens = Regex.Split(line, "\\s+");
                if (tokens.Length > 4)
                {
                    int test = -1;
                    int.TryParse(tokens[5], out test);

                    if (test > 1023)
                    {
                        try
                        {
                            var p = System.Diagnostics.Process.GetProcessById(test);
                            if (p.ProcessName == "Unity")
                            {
                                return test;
                            }
                        }
                        catch
                        {

                        }
                    }
                }
            }
#else
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "lsof";
            process.StartInfo.Arguments = "-c /^Unity$/ -i 4tcp -a";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            // Not thread safe (yet!)
            string output = process.StandardOutput.ReadToEnd();
            string[] lines = output.Split('\n');

            process.WaitForExit();

            foreach (string line in lines)
            {
                int port = -1;
                if (line.StartsWith("Unity"))
                {
                    string[] portions = line.Split(new string[] { "TCP *:" }, System.StringSplitOptions.None);
                    if (portions.Length >= 2)
                    {
                        Regex digitsOnly = new Regex(@"[^\d]");
                        string cleanPort = digitsOnly.Replace(portions[1], "");
                        if (int.TryParse(cleanPort, out port))
                        {
                            if (port > -1)
                            {
                                return port;
                            }
                        }
                    }
                }
            }
#endif
            return -1;
        }


        static void InstallUnityDebugger()
        {
            EditorUtility.DisplayProgressBar("VSCode", "Downloading Unity Debugger ...", 0.1f);
            byte[] fileContent;
            
            try
            {
                using (var webClient = new System.Net.WebClient())
                {
                    fileContent = webClient.DownloadData(UnityDebuggerURL);
                }
            }
            catch (Exception e)
            {
                if (Debug)
                {
                    UnityEngine.Debug.Log("[VSCode] " + e.Message);

                }

                // Don't go any further if there is an error
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            // Do we have a file to install?
            if ( fileContent != null ) {
                string fileName = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".vsix";
                File.WriteAllBytes(fileName, fileContent);
                
                CallVSCode(fileName);
            }

        }
    
        // HACK: This is in until Unity can figure out why MD keeps opening even though a different program is selected.
        [MenuItem("Assets/Open C# Project In Code", false, 1000)]
        static void MenuOpenProject()
        {
            // Force the project files to be sync
            SyncSolution();

            // Load Project
            CallVSCode("\"" + ProjectPath + "\" -r");
        }

        [MenuItem("Assets/Open C# Project In Code", true, 1000)]
        static bool ValidateMenuOpenProject()
        {
            return Enabled;
        }

        /// <summary>
        /// VS Code Integration Preferences Item
        /// </summary>
        /// <remarks>
        /// Contains all 3 toggles: Enable/Disable; Debug On/Off; Writing Launch File On/Off
        /// </remarks>
        [PreferenceItem("VSCode")]
        static void VSCodePreferencesItem()
        {
            if (EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox("Please wait for Unity to finish compiling. \nIf the window doesn't refresh, simply click on the window or move it around to cause a repaint to happen.", MessageType.Warning);
                return;
            }
            EditorGUILayout.BeginVertical();

            EditorGUILayout.HelpBox("Support development of this plugin, follow @reapazor and @dotbunny on Twitter.", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            Enabled = EditorGUILayout.Toggle(new GUIContent("Enable Integration", "Should the integration work its magic for you?"), Enabled);
            UseUnityDebugger = EditorGUILayout.Toggle(new GUIContent("Use Unity Debugger", "Should the integration integrate with Unity's VSCode Extension (must be installed)."), UseUnityDebugger);

            EditorGUILayout.Space();
            RevertExternalScriptEditorOnExit = EditorGUILayout.Toggle(new GUIContent("Revert Script Editor On Unload", "Should the external script editor setting be reverted to its previous setting on project unload? This is useful if you do not use Code with all your projects."),RevertExternalScriptEditorOnExit);
            
            Debug = EditorGUILayout.Toggle(new GUIContent("Output Messages To Console", "Should informational messages be sent to Unity's Console?"), Debug);

            WriteLaunchFile = EditorGUILayout.Toggle(new GUIContent("Always Write Launch File", "Always write the launch.json settings when entering play mode?"), WriteLaunchFile);

            EditorGUILayout.Space();

            AutomaticUpdates = EditorGUILayout.Toggle(new GUIContent("Automatic Updates", "Should the plugin automatically update itself?"), AutomaticUpdates);

            UpdateTime = EditorGUILayout.IntSlider(new GUIContent("Update Timer (Days)", "After how many days should updates be checked for?"), UpdateTime, 1, 31);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                UpdateUnityPreferences(Enabled);

                //UnityEditor.PreferencesWindow.Read

                // TODO: Force Unity To Reload Preferences
                // This seems to be a hick up / issue

                if (VSCode.Debug)
                {
                    if (Enabled)
                    {
                        UnityEngine.Debug.Log("[VSCode] Integration Enabled");
                    }
                    else
                    {
                        UnityEngine.Debug.Log("[VSCode] Integration Disabled");
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("Force Update", "Check for updates to the plugin, right NOW!")))
            {
                CheckForUpdate();
                EditorGUILayout.EndVertical();
                return;
            }
            if (GUILayout.Button(new GUIContent("Write Workspace Settings", "Output a default set of workspace settings for VSCode to use, ignoring many different types of files.")))
            {
                WriteWorkspaceSettings();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.Space();
            EditorGUILayout.Space();


            if (UseUnityDebugger)
            {
                EditorGUILayout.HelpBox("In order for the \"Use Unity Debuggger\" option to function above, you need to have installed the Unity Debugger Extension for Visual Studio Code. You can do this by simply clicking the button below and it will take care of the rest.", MessageType.Warning);
                if (GUILayout.Button(new GUIContent("Install Unity Debugger", "Install the Unity Debugger Extension into Code")))
                {
                    InstallUnityDebugger();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label(
                new GUIContent(
                    string.Format("{0:0.00}", Version) + VersionCode,
                    "GitHub's Version @ " + string.Format("{0:0.00}", GitHubVersion)));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

        }

        /// <summary>
        /// Asset Open Callback (from Unity)
        /// </summary>
        /// <remarks>
        /// Called when Unity is about to open an asset.
        /// </remarks>
        [UnityEditor.Callbacks.OnOpenAssetAttribute()]
        static bool OnOpenedAsset(int instanceID, int line)
        {
            // bail out if we are not using VSCode
            if (!Enabled)
            {
                return false;
            }

            // current path without the asset folder
            string appPath = ProjectPath;

            // determine asset that has been double clicked in the project view
            UnityEngine.Object selected = EditorUtility.InstanceIDToObject(instanceID);

            if (selected.GetType().ToString() == "UnityEditor.MonoScript")
            {
                string completeFilepath = appPath + Path.DirectorySeparatorChar + AssetDatabase.GetAssetPath(selected);

                string args = null;
                if (line == -1)
                {

                    args = "\"" + ProjectPath + "\" \"" + completeFilepath + "\" -r";
                }
                else
                {
                    args = "\"" + ProjectPath + "\" -g \"" + completeFilepath + ":" + line.ToString() + "\" -r";
                }
                // call 'open'
                CallVSCode(args);

                return true;
            }

            // Didnt find a code file? let Unity figure it out
            return false;

        }

        /// <summary>
        /// Executed when the Editor's playmode changes allowing for capture of required data
        /// </summary>
        static void OnPlaymodeStateChanged()
        {
            if (UnityEngine.Application.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UpdateLaunchFile();
            }
        }

        /// <summary>
        /// Detect when scripts are reloaded and relink playmode detection
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts()]
        static void OnScriptReload()
        {
            EditorApplication.playmodeStateChanged -= OnPlaymodeStateChanged;
            EditorApplication.playmodeStateChanged += OnPlaymodeStateChanged;
        }

        /// <summary>
        /// Remove extra/erroneous lines from a file.
        static void ScrubFile(string path)
        {
            string[] lines = File.ReadAllLines(path);
            System.Collections.Generic.List<string> newLines = new System.Collections.Generic.List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                // Check Empty
                if (string.IsNullOrEmpty(lines[i].Trim()) || lines[i].Trim() == "\t" || lines[i].Trim() == "\t\t")
                {

                }
                else
                {
                    newLines.Add(lines[i]);
                }
            }
            File.WriteAllLines(path, newLines.ToArray());
        }

        /// <summary>
        /// Remove extra/erroneous data from project file (content).
        /// </summary>
        static string ScrubProjectContent(string content)
        {
            if (content.Length == 0)
                return "";

            // Note: it causes OmniSharp faults on Windows, such as "not seeing UnityEngine.UI". 3.5 target works fine
#if !UNITY_EDITOR_WIN
            // Make sure our reference framework is 2.0, still the base for Unity
            if (content.IndexOf("<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>") != -1)
            {
                content = Regex.Replace(content, "<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>", "<TargetFrameworkVersion>v2.0</TargetFrameworkVersion>");
            }
#endif

            string targetPath = "";// "<TargetPath>Temp" + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar + "</TargetPath>"; //OutputPath
            string langVersion = "<LangVersion>default</LangVersion>";


            bool found = true;
            int location = 0;
            string addedOptions = "";
            int startLocation = -1;
            int endLocation = -1;
            int endLength = 0;

            while (found)
            {
                startLocation = -1;
                endLocation = -1;
                endLength = 0;
                addedOptions = "";
                startLocation = content.IndexOf("<PropertyGroup", location);

                if (startLocation != -1)
                {

                    endLocation = content.IndexOf("</PropertyGroup>", startLocation);
                    endLength = (endLocation - startLocation);


                    if (endLocation == -1)
                    {
                        found = false;
                        continue;
                    }
                    else
                    {
                        found = true;
                        location = endLocation;
                    }

                    if (content.Substring(startLocation, endLength).IndexOf("<TargetPath>") == -1)
                    {
                        addedOptions += "\n\r\t" + targetPath + "\n\r";
                    }

                    if (content.Substring(startLocation, endLength).IndexOf("<LangVersion>") == -1)
                    {
                        addedOptions += "\n\r\t" + langVersion + "\n\r";
                    }

                    if (!string.IsNullOrEmpty(addedOptions))
                    {
                        content = content.Substring(0, endLocation) + addedOptions + content.Substring(endLocation);
                    }
                }
                else
                {
                    found = false;
                }
            }

            return content;
        }

        /// <summary>
        /// Remove extra/erroneous data from solution file (content).
        /// </summary>
        static string ScrubSolutionContent(string content)
        {
            // Replace Solution Version
            content = content.Replace(
                "Microsoft Visual Studio Solution File, Format Version 11.00\r\n# Visual Studio 2008\r\n",
                "\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n# Visual Studio 2012");

            // Remove Solution Properties (Unity Junk)
            int startIndex = content.IndexOf("GlobalSection(SolutionProperties) = preSolution");
            if (startIndex != -1)
            {
                int endIndex = content.IndexOf("EndGlobalSection", startIndex);
                content = content.Substring(0, startIndex) + content.Substring(endIndex + 16);
            }


            return content;
        }
        
       
        /// <summary>
        /// Update Visual Studio Code Launch file
        /// </summary>
        static void UpdateLaunchFile()
        {
            if ( !VSCode.Enabled ) return;
            
            else if ( VSCode.UseUnityDebugger ) {
                if (!Directory.Exists(VSCode.SettingsFolder))
                        System.IO.Directory.CreateDirectory(VSCode.SettingsFolder);

                // Write out proper formatted JSON (hence no more SimpleJSON here)
                string fileContent = "{\n\t\"version\": \"0.2.0\",\n\t\"configurations\": [\n\t\t{\n\t\t\t\"name\": \"Unity Editor\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"Windows Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"OSX Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"Linux Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"iOS Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\t\t},\n\t\t{\n\t\t\t\"name\": \"Android Player\",\n\t\t\t\"type\": \"unity\",\n\t\t\t\"request\": \"launch\"\n\n\t\t}\n\t]\n}";
                File.WriteAllText(VSCode.LaunchPath, fileContent);
            }
            
            else if (VSCode.WriteLaunchFile)
            {
                int port = GetDebugPort();
                if (port > -1)
                {
                    if (!Directory.Exists(VSCode.SettingsFolder))
                        System.IO.Directory.CreateDirectory(VSCode.SettingsFolder);

                    // Write out proper formatted JSON (hence no more SimpleJSON here)
                    string fileContent = "{\n\t\"version\":\"0.2.0\",\n\t\"configurations\":[ \n\t\t{\n\t\t\t\"name\":\"Unity\",\n\t\t\t\"type\":\"mono\",\n\t\t\t\"request\":\"attach\",\n\t\t\t\"address\":\"localhost\",\n\t\t\t\"port\":" + port + "\n\t\t}\n\t]\n}";
                    File.WriteAllText(VSCode.LaunchPath, fileContent);

                    if (VSCode.Debug)
                    {
                        UnityEngine.Debug.Log("[VSCode] Debug Port Found (" + port + ")");
                    }
                }
                else
                {
                    if (VSCode.Debug)
                    {
                        UnityEngine.Debug.LogWarning("[VSCode] Unable to determine debug port.");
                    }
                }
            }
        }

        /// <summary>
        /// Update Unity Editor Preferences
        static void UpdateUnityPreferences(bool enabled)
        {
            if (enabled)
            {
#if UNITY_EDITOR_OSX
                var newPath = "/Applications/Visual Studio Code.app";
#elif UNITY_EDITOR_WIN
                var newPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "Code" + Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar + "code.cmd";
#else
                var newPath = "/usr/local/bin/code";
#endif

                // App
                if (EditorPrefs.GetString("kScriptsDefaultApp") != newPath)
                {
                    EditorPrefs.SetString("VSCode_PreviousApp", EditorPrefs.GetString("kScriptsDefaultApp"));
                }
                EditorPrefs.SetString("kScriptsDefaultApp", newPath);

                // Arguments
                if (EditorPrefs.GetString("kScriptEditorArgs") != "-r -g \"$(File):$(Line)\"")
                {
                    EditorPrefs.SetString("VSCode_PreviousArgs", EditorPrefs.GetString("kScriptEditorArgs"));
                }

                EditorPrefs.SetString("kScriptEditorArgs", "-r -g \"$(File):$(Line)\"");
                EditorPrefs.SetString("kScriptEditorArgs" + newPath, "-r -g \"$(File):$(Line)\"");


                // MonoDevelop Solution
                if (EditorPrefs.GetBool("kMonoDevelopSolutionProperties", false))
                {
                    EditorPrefs.SetBool("VSCode_PreviousMD", true);
                }
                EditorPrefs.SetBool("kMonoDevelopSolutionProperties", false);

                // Support Unity Proj (JS)
                if (EditorPrefs.GetBool("kExternalEditorSupportsUnityProj", false))
                {
                    EditorPrefs.SetBool("VSCode_PreviousUnityProj", true);
                }
                EditorPrefs.SetBool("kExternalEditorSupportsUnityProj", false);

                // Attach to Editor
                if (!EditorPrefs.GetBool("AllowAttachedDebuggingOfEditor", false))
                {
                    EditorPrefs.SetBool("VSCode_PreviousAttach", false);
                }
                EditorPrefs.SetBool("AllowAttachedDebuggingOfEditor", true);
            }
            else
            {

                // Restore previous app
                if (!string.IsNullOrEmpty(EditorPrefs.GetString("VSCode_PreviousApp")))
                {
                    EditorPrefs.SetString("kScriptsDefaultApp", EditorPrefs.GetString("VSCode_PreviousApp"));
                }

                // Restore previous args
                if (!string.IsNullOrEmpty(EditorPrefs.GetString("VSCode_PreviousArgs")))
                {
                    EditorPrefs.SetString("kScriptEditorArgs", EditorPrefs.GetString("VSCode_PreviousArgs"));
                }

                // Restore MD setting
                if (EditorPrefs.GetBool("VSCode_PreviousMD", false))
                {
                    EditorPrefs.SetBool("kMonoDevelopSolutionProperties", true);
                }

                // Restore MD setting
                if (EditorPrefs.GetBool("VSCode_PreviousUnityProj", false))
                {
                    EditorPrefs.SetBool("kExternalEditorSupportsUnityProj", true);
                }


                // Restore previous attach
                if (!EditorPrefs.GetBool("VSCode_PreviousAttach", true))
                {
                    EditorPrefs.SetBool("AllowAttachedDebuggingOfEditor", false);
                }
            }

            FixUnityPreferences();
        }

        /// <summary>
        /// Write Default Workspace Settings
        /// </summary>
        static void WriteWorkspaceSettings()
        {
            if (Debug)
            {
                UnityEngine.Debug.Log("[VSCode] Workspace Settings Written");
            }

            if (!Directory.Exists(VSCode.SettingsFolder))
            {
                System.IO.Directory.CreateDirectory(VSCode.SettingsFolder);
            }

            string exclusions =
                "{\n" +
                "\t\"files.exclude\":\n" +
                "\t{\n" +
                // Hidden Files
                "\t\t\"**/.DS_Store\":true,\n" +
                "\t\t\"**/.git\":true,\n" +
                "\t\t\"**/.gitignore\":true,\n" +
                "\t\t\"**/.gitattributes\":true,\n" +
                "\t\t\"**/.gitmodules\":true,\n" +
                "\t\t\"**/.svn\":true,\n" +


                // Project Files
                "\t\t\"**/*.booproj\":true,\n" +
                "\t\t\"**/*.pidb\":true,\n" +
                "\t\t\"**/*.suo\":true,\n" +
                "\t\t\"**/*.user\":true,\n" +
                "\t\t\"**/*.userprefs\":true,\n" +
                "\t\t\"**/*.unityproj\":true,\n" +
                "\t\t\"**/*.dll\":true,\n" +
                "\t\t\"**/*.exe\":true,\n" +

                // Media Files
                "\t\t\"**/*.pdf\":true,\n" +

                // Audio
                "\t\t\"**/*.mid\":true,\n" +
                "\t\t\"**/*.midi\":true,\n" +
                "\t\t\"**/*.wav\":true,\n" +

                // Textures
                "\t\t\"**/*.gif\":true,\n" +
                "\t\t\"**/*.ico\":true,\n" +
                "\t\t\"**/*.jpg\":true,\n" +
                "\t\t\"**/*.jpeg\":true,\n" +
                "\t\t\"**/*.png\":true,\n" +
                "\t\t\"**/*.psd\":true,\n" +
                "\t\t\"**/*.tga\":true,\n" +
                "\t\t\"**/*.tif\":true,\n" +
                "\t\t\"**/*.tiff\":true,\n" +

                // Models
                "\t\t\"**/*.3ds\":true,\n" +
                "\t\t\"**/*.3DS\":true,\n" +
                "\t\t\"**/*.fbx\":true,\n" +
                "\t\t\"**/*.FBX\":true,\n" +
                "\t\t\"**/*.lxo\":true,\n" +
                "\t\t\"**/*.LXO\":true,\n" +
                "\t\t\"**/*.ma\":true,\n" +
                "\t\t\"**/*.MA\":true,\n" +
                "\t\t\"**/*.obj\":true,\n" +
                "\t\t\"**/*.OBJ\":true,\n" +

                // Unity File Types
                "\t\t\"**/*.asset\":true,\n" +
                "\t\t\"**/*.cubemap\":true,\n" +
                "\t\t\"**/*.flare\":true,\n" +
                "\t\t\"**/*.mat\":true,\n" +
                "\t\t\"**/*.meta\":true,\n" +
                "\t\t\"**/*.prefab\":true,\n" +
                "\t\t\"**/*.unity\":true,\n" +

                // Folders
                "\t\t\"build/\":true,\n" +
                "\t\t\"Build/\":true,\n" +
                "\t\t\"Library/\":true,\n" +
                "\t\t\"library/\":true,\n" +
                "\t\t\"obj/\":true,\n" +
                "\t\t\"Obj/\":true,\n" +
                "\t\t\"ProjectSettings/\":true,\r" +
                "\t\t\"temp/\":true,\n" +
                "\t\t\"Temp/\":true\n" +
                "\t}\n" +
                "}";

            // Dont like the replace but it fixes the issue with the JSON
            File.WriteAllText(VSCode.SettingsPath, exclusions);
        }

        #endregion
    }



    /// <summary>
    /// VSCode Asset AssetPostprocessor
    /// <para>This will ensure any time that the project files are generated the VSCode versions will be made</para>
    /// </summary>
    /// <remarks>Undocumented Event</remarks>
    public class VSCodeAssetPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// On documented, project generation event callback
        /// </summary>
        private static void OnGeneratedCSProjectFiles()
        {
            // Force execution of VSCode update
            VSCode.UpdateSolution();
        }
    }
}
