/*
 * Unity VSCode Support
 *
 * Seamless support for Microsoft Visual Studio Code in Unity
 *
 * Version:
 *   1.95
 *
 * Authors:
 *   Matthew Davey <matthew.davey@dotbunny.com>
 */
// REQUIRES: VSCode 0.8.0 - Settings directory moved to .vscode
// TODO: Currently VSCode will not debug mono on Windows -- need a solution.
namespace dotBunny.Unity
{
    using System.IO;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    public static class VSCode
    {
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
                EditorPrefs.SetBool("VSCode_Enabled", value);
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
        /// Quick reference to the VSCode settings folder
        /// </summary>
        static string SettingsFolder
        {
            get
            {
                return ProjectPath + System.IO.Path.DirectorySeparatorChar + ".vscode";
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

        static string SettingsPath
        {
            get
            {
                return SettingsFolder + System.IO.Path.DirectorySeparatorChar + "settings.json";
            }
        }



        #endregion

        /// <summary>
        /// Fail safe integration constructor
        /// </summary>
        static VSCode()
        {
            if (Enabled)
            {
                UpdateUnityPreferences(true);
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
            proc.StartInfo.FileName = "code";
            proc.StartInfo.Arguments = args;
            proc.StartInfo.UseShellExecute = false;
#else
            //TODO: Allow for manual path to code?
            proc.StartInfo.FileName = "code";
            proc.StartInfo.Arguments = args;
            proc.StartInfo.UseShellExecute = false;
#endif
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();
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
            System.Diagnostics.Process process = new System.Diagnostics.Process ();
            process.StartInfo.FileName = "lsof";
            process.StartInfo.Arguments = "-c /^Unity$/ -i 4tcp -a";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start ();

            // Not thread safe (yet!)
            string output = process.StandardOutput.ReadToEnd ();
            string[] lines = output.Split ('\n');

            process.WaitForExit ();

            foreach (string line in lines) {
                int port = -1;
                if (line.StartsWith ("Unity")) {
                    string[] portions = line.Split (new string[] { "TCP *:" }, System.StringSplitOptions.None);
                    if (portions.Length >= 2) {
                        Regex digitsOnly = new Regex (@"[^\d]");
                        string cleanPort = digitsOnly.Replace (portions [1], "");
                        if (int.TryParse (cleanPort, out port)) {
                            if (port > -1) {
                                return port;
                            }
                        }
                    }
                }
            }
#endif
            return -1;
        }

        /// <summary>
        /// VS Code Integration Preferences Item
        /// </summary>
        /// <remarks>
        /// Contains all 3 toggles: Enable/Disable; Debug On/Off; Writing Launch File On/Off
        /// </remarks>
        [PreferenceItem( "VSCode" )]
        static void VSCodePreferencesItem()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.HelpBox("Support development of this plugin, follow @reapazor and @dotbunny on Twitter.", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            Enabled = EditorGUILayout.Toggle("Enable Integration", Enabled);

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(!Enabled);
            Debug = EditorGUILayout.Toggle("Output Messages To Console", Debug);

            WriteLaunchFile = EditorGUILayout.Toggle("Always Write Launch File", WriteLaunchFile);

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            if( EditorGUI.EndChangeCheck())
            {
                UpdateUnityPreferences(Enabled);
                if (VSCode.Debug)
                {
                    if (Enabled)
                    {
                        UnityEngine.Debug.Log("[VSCode] Integration Enabled");
                    }
                    else {
                        UnityEngine.Debug.Log("[VSCode] Integration Disabled");
                    }
                }
            }
            
            if (GUILayout.Button("Write Workspace Settings"))
            {
                WriteWorkspaceSettings();
            }
            
            EditorGUILayout.EndVertical();
        }

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
        /// Asset Open Callback (from Unity)
        /// </summary>
        /// <remarks>
        /// Called when Unity is about to open an asset.
        /// </remarks>
        [UnityEditor.Callbacks.OnOpenAssetAttribute()]
        static bool OnOpenedAsset(int instanceID, int line)
        {
            // bail out if we are not on a Mac or if we don't want to use VSCode
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
            if (VSCode.Enabled && VSCode.WriteLaunchFile && UnityEngine.Application.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                int port = GetDebugPort();
                if (port > -1)
                {
                    if (!Directory.Exists(VSCode.SettingsFolder))
                        System.IO.Directory.CreateDirectory(VSCode.SettingsFolder);
                    UpdateLaunchFile(port);

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

            // Make sure our reference framework is 2.0, still the base for Unity
            if (content.IndexOf("<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>") != -1)
            {
                content = Regex.Replace(content, "<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>", "<TargetFrameworkVersion>v2.0</TargetFrameworkVersion>");
            }

            string targetPath = "<TargetPath>Temp\\bin\\Debug\\</TargetPath>"; //OutputPath
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
        static void UpdateLaunchFile(int port)
        {
            // Write out proper formatted JSON (hence no more SimpleJSON here)
            string fileContent = "{\n\t\"version\":\"0.1.0\",\n\t\"configurations\":[ \n\t\t{\n\t\t\t\"name\":\"Unity\",\n\t\t\t\"type\":\"mono\",\n\t\t\t\"address\":\"localhost\",\n\t\t\t\"port\":" + port + "\n\t\t}\n\t]\n}";
            File.WriteAllText(VSCode.LaunchPath, fileContent);
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
        }

        /// <summary>
        /// Write Default Workspace Settings
        /// </summary>
        static void WriteWorkspaceSettings()
        {
            if (Debug)
            {
                UnityEngine.Debug.Log("[VSCode] Workspace Settigns Written");
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
