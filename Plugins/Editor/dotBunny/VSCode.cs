/*
 * Unity VSCode Support
 *
 * Seamless support for Microsoft Visual Studio Code in Unity
 *
 * Version: 
 *   1.6.5
 *
 * Authors:
 *   Matthew Davey <matthew.davey@dotbunny.com>
 */

// TODO: Currently VSCode will not debug mono on Windows -- need a solution.
// TODO: Remove reliance on SimpleJSON - Unity 5.3 JSON serializer
namespace dotBunny.Unity
{
	using System.IO;
	using System.Text.RegularExpressions;
	using UnityEditor;

	public static class VSCode
	{
		#region Properties

		/// <summary>
		/// Should debug information be displayed in the Unity terminal?
		/// </summary>
		public static bool Debug {
			get {
				return EditorPrefs.GetBool ("VSCode_Debug", false);
			}
			set {
				EditorPrefs.SetBool ("VSCode_Debug", value);
			}
		}

		/// <summary>
		/// Is the Visual Studio Code Integration Enabled?
		/// </summary>
		/// <remarks>
		/// We do not want to automatically turn it on, for in larger projects not everyone is using VSCode
		/// </remarks>
		public static bool Enabled {
			get {
				return EditorPrefs.GetBool ("VSCode_Enabled", false);
			}
			set {
				EditorPrefs.SetBool ("VSCode_Enabled", value);
			}
		}
		
        /// <summary>
		/// Quick reference to the VSCode settings folder
		/// </summary>
		static string LaunchFolder {
			get {
				return ProjectPath + System.IO.Path.DirectorySeparatorChar + ".settings";
			}
		}
        
		/// <summary>
		/// Quick reference to the VSCode launch settings file
		/// </summary>
		static string LaunchPath {
			get {
				return LaunchFolder + System.IO.Path.DirectorySeparatorChar + "launch.json";
			}
		}

		/// <summary>
		/// The full path to the project
		/// </summary>
		static string ProjectPath {
			get {
				return System.IO.Path.GetDirectoryName (UnityEngine.Application.dataPath);
			}
		}

		#endregion

		#region Public Members

		/// <summary>
		/// Force Unity To Write Project File
		/// </summary>
		/// <remarks>
		/// Reflection!
		/// </remarks>
		public static void SyncSolution ()
		{
			System.Type T = System.Type.GetType ("UnityEditor.SyncVS,UnityEditor");
			System.Reflection.MethodInfo SyncSolution = T.GetMethod ("SyncSolution", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			SyncSolution.Invoke (null, null);
		}

		/// <summary>
		/// Update the solution files so that they work with VS Code
		/// </summary>
		public static void UpdateSolution ()
		{
			var currentDirectory = Directory.GetCurrentDirectory ();
			var solutionFiles = Directory.GetFiles (currentDirectory, "*.sln");
			var projectFiles = Directory.GetFiles (currentDirectory, "*.csproj");

			foreach (var filePath in solutionFiles) {
				string content = File.ReadAllText (filePath);
				content = ScrubSolutionContent (content);
				File.WriteAllText (filePath, content);
				ScrubFile (filePath);
			}

			foreach (var filePath in projectFiles) {
				string content = File.ReadAllText (filePath);
				content = ScrubProjectContent (content);
				File.WriteAllText (filePath, content);
				ScrubFile (filePath);
			}
		}

		#endregion

		#region Private Members

		/// <summary>
		/// Call VSCode with arguements
		/// </summary>
		static void CallVSCode (string args)
		{
			System.Diagnostics.Process proc = new System.Diagnostics.Process ();

			if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor) {
				UnityEngine.Debug.Log("code " + args);
				proc.StartInfo.FileName = "code";
				proc.StartInfo.Arguments = args;
				proc.StartInfo.UseShellExecute = false;
				
			} else {
				proc.StartInfo.FileName = "open";
				proc.StartInfo.Arguments = " -n -b \"com.microsoft.VSCode\" --args " + args;
				proc.StartInfo.UseShellExecute = false;
			}

			proc.StartInfo.RedirectStandardOutput = true;
			proc.Start ();
		}

		/// <summary>
		/// Determine what port Unity is listening for on Mac
		/// </summary>
		static int GetMacDebugPort ()
		{
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
			return -1;
		}

		/// <summary>
		/// Determine what port Unity is listening for on Windows
		/// </summary>
		static int GetWindowsDebugPort ()
		{
			System.Diagnostics.Process process = new System.Diagnostics.Process ();
			process.StartInfo.FileName = "netstat";
			process.StartInfo.Arguments = "-a -n -o -p TCP";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.Start ();
            
			string output = process.StandardOutput.ReadToEnd ();
			string[] lines = output.Split ('\n');
            
			process.WaitForExit ();
            
			foreach (string line in lines) {
				int port = -1;
				string[] tokens = Regex.Split (line, "\\s+");
				if (tokens.Length > 4) {
					string localAddress = Regex.Replace (tokens [2], @"\[(.*?)\]", "1.1.1.1");
					
					int test = -1;
					int.TryParse(tokens[5], out test);
					
					if ( test > 1023 ) {
						try {
							var p = System.Diagnostics.Process.GetProcessById (test);
							if (p.ProcessName == "Unity") {
								return test;
							}
						} catch {
							
						}
					}
				}
			}
			return -1;
		}

		[MenuItem ("Assets/VS Code/Enable Debug")]
		static void MenuEnableDebug ()
		{
			Menu.SetChecked ("Assets/VS Code/Enable Debug", !Debug);
			Debug = !Debug;
		}

		[MenuItem ("Assets/VS Code/Enable Integration")]
		static void MenuEnableIntegration ()
		{
			Menu.SetChecked ("Assets/VS Code/Enable Integration", !Enabled);
			Enabled = !Enabled;
		}

		[MenuItem ("Assets/VS Code/Force Sync Project #%v", false, 201)]
		public static void MenuForceSyncProject ()
		{
			// Calling this will then trigger the callback as well;
			SyncSolution ();
		}

		[MenuItem ("Assets/VS Code/Open Project", false, 100)]
		static void MenuOpenProject ()
		{
			CallVSCode ("\"" + ProjectPath + "\" -r");
		}

		/// <summary>
		/// Update "Enable Debug" menu item
		/// </summary>
		[MenuItem ("Assets/VS Code/Enable Debug", true, 301)]
		static bool MenuValidateMenuEnableDebug ()
		{
			Menu.SetChecked ("Assets/VS Code/Enable Debug", Debug);
			return true;
		}

       
		/// <summary>
		/// Update "Enable Integration" menu item
		/// </summary>
		[MenuItem ("Assets/VS Code/Enable Integration", true, 300)]
		static bool MenuValidateMenuEnableIntegration ()
		{
			Menu.SetChecked ("Assets/VS Code/Enable Integration", Enabled);
			return true;
		}

		/// <summary>
		/// Asset Open Callback (from Unity)
		/// </summary>
		/// <remarks>
		/// Called when Unity is about to open an asset.
		/// </remarks>
		[UnityEditor.Callbacks.OnOpenAssetAttribute ()]
		static bool OnOpenedAsset (int instanceID, int line)
		{
			// bail out if we are not on a Mac or if we don't want to use VSCode
			if (!Enabled) {
				return false;
			}

			// current path without the asset folder
			string appPath = ProjectPath;

			// determine asset that has been double clicked in the project view
			UnityEngine.Object selected = EditorUtility.InstanceIDToObject (instanceID);

			if (selected.GetType ().ToString () == "UnityEditor.MonoScript") {
				string completeFilepath = appPath + Path.DirectorySeparatorChar + AssetDatabase.GetAssetPath (selected);

				string args = null;
				if (line == -1) {
					args = "\"" + completeFilepath + "\" -r";
				} else {
					args = "-g \"" + completeFilepath + ":" + line.ToString () + "\" -r";
				}
				// call 'open'
				CallVSCode (args);

				return true;
			}

			// Didnt find a code file? let Unity figure it out
			return false;
		}

		/// <summary>
		/// Executed when the Editor's playmode changes allowing for capture of required data
		/// </summary>
		static void OnPlaymodeStateChanged ()
		{
			if (VSCode.Enabled && UnityEngine.Application.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {
				int port = -1;
				if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor) {
					port = GetWindowsDebugPort ();
				} else {
					port = GetMacDebugPort ();
				}
                
				if (port > -1) {

					UpdateLaunchFile (port);

					if (VSCode.Debug) {
						UnityEngine.Debug.Log ("[VSCode] Debug Port Found (" + port + ")");
					}
				} else {
					if (VSCode.Debug) {
						UnityEngine.Debug.LogWarning ("[VSCode] Unable to determine debug port.");
					}
				} 
			}
		}

		/// <summary>
		/// Detect when scripts are reloaded and relink playmode detection
		/// </summary>
		[UnityEditor.Callbacks.DidReloadScripts ()]
		static void OnScriptReload ()
		{
			EditorApplication.playmodeStateChanged -= OnPlaymodeStateChanged;
			EditorApplication.playmodeStateChanged += OnPlaymodeStateChanged;
		}

        
		/// <summary>
		/// Remove extra/erroneous lines from a file.
		static void ScrubFile (string path)
		{
			string[] lines = File.ReadAllLines (path);
			System.Collections.Generic.List<string> newLines = new System.Collections.Generic.List<string> ();
			for (int i = 0; i < lines.Length; i++) {
				// Check Empty
				if (string.IsNullOrEmpty (lines [i].Trim ()) || lines [i].Trim () == "\t" || lines [i].Trim () == "\t\t") {

				} else {
					newLines.Add (lines [i]);
				}
			}
			File.WriteAllLines (path, newLines.ToArray ());
		}

		/// <summary>
		/// Remove extra/erroneous data from project file (content). 
		/// </summary>
		static string ScrubProjectContent (string content)
		{
			if ( content.Length == 0 ) return "";
			
			// Make sure our reference framework is 2.0, still the base for Unity
			if (content.IndexOf ("<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>") != -1) {
				content = Regex.Replace (content, "<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>", "<TargetFrameworkVersion>v2.0</TargetFrameworkVersion>");
			}

			string targetPath = "<TargetPath>Temp\\bin\\Debug\\</TargetPath>"; //OutputPath
			string langVersion = "<LangVersion>default</LangVersion>";


			bool found = true;
			int location = 0;
			string addedOptions = "";
			int startLocation = -1;
			int endLocation = -1;
			int endLength = 0;

			while (found) {
				startLocation = -1;
				endLocation = -1;
				endLength = 0;
				addedOptions = "";
				startLocation = content.IndexOf ("<PropertyGroup", location);

				if (startLocation != -1) {

					endLocation = content.IndexOf ("</PropertyGroup>", startLocation);
					endLength = (endLocation - startLocation);
                   

					if (endLocation == -1) {
						found = false;
						continue;
					} else {
						found = true;
						location = endLocation;
					}
					
					if (content.Substring (startLocation, endLength).IndexOf ("<TargetPath>") == -1) {
						addedOptions += "\n\r\t" + targetPath + "\n\r";
					}

					if (content.Substring (startLocation, endLength).IndexOf ("<LangVersion>") == -1) {
						addedOptions += "\n\r\t" + langVersion + "\n\r";
					}

					if (!string.IsNullOrEmpty (addedOptions)) {
						content = content.Substring (0, endLocation) + addedOptions + content.Substring (endLocation);
					}
				} else {
					found = false;
				}
			}

			return content;
		}

		/// <summary>
		/// Remove extra/erroneous data from solution file (content). 
		/// </summary>
		static string ScrubSolutionContent (string content)
		{
			// Replace Solution Version
			content = content.Replace (
				"Microsoft Visual Studio Solution File, Format Version 11.00\r\n# Visual Studio 2008\r\n",
				"\r\nMicrosoft Visual Studio Solution File, Format Version 12.00\r\n# Visual Studio 2012");

			// Remove Solution Properties (Unity Junk)
			int startIndex = content.IndexOf ("GlobalSection(SolutionProperties) = preSolution");
			if (startIndex != -1) {
				int endIndex = content.IndexOf ("EndGlobalSection", startIndex);
				content = content.Substring (0, startIndex) + content.Substring (endIndex + 16);
			}


			return content;
		}

		/// <summary>
		/// Updte Visual Studio Code Launch file
		/// </summary>
		static void UpdateLaunchFile (int port)
		{
			//TODO Eventually all this JSON can be replaced with intragrated JSON
            
			// Create Default Config
			SimpleJSON.JSONClass defaultClass = new SimpleJSON.JSONClass ();
			defaultClass ["name"] = "Unity";
			defaultClass ["type"] = "mono";
			defaultClass ["address"] = "localhost";
			defaultClass ["port"].AsInt = port;
			defaultClass ["sourceMaps"].AsBool = false;
            
			// Create Default Node
			SimpleJSON.JSONNode defaultNode = new SimpleJSON.JSONClass ();
			defaultNode ["version"] = "0.1.0";
			defaultNode ["configurations"] [-1] = defaultClass;

			if (!Directory.Exists(VSCode.LaunchFolder)) {
				System.IO.Directory.CreateDirectory(VSCode.LaunchFolder);
			}
			
			if (!File.Exists (VSCode.LaunchPath)) {
				File.WriteAllText (VSCode.LaunchPath, defaultNode.ToString ());
			} else {
				string rawContent = File.ReadAllText (VSCode.LaunchPath);
				SimpleJSON.JSONNode existingNode = SimpleJSON.JSON.Parse (rawContent);

				bool found = false;
                
				if (existingNode != null && existingNode ["configurations"] != null) {
					int index = 0;
                    
					foreach (SimpleJSON.JSONNode conf in existingNode["configurations"].AsArray) {
						if (conf ["name"].Value == "Unity") {
							found = true;
							break;
						}
						index++;
					}
    
					if (found) {
						existingNode ["configurations"] [index] = defaultClass;
					}
				}
                
				if (found) {
					File.WriteAllText (VSCode.LaunchPath, existingNode.ToString ());
				} else {
					File.WriteAllText (VSCode.LaunchPath, defaultNode.ToString ());
				}
			} 
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
		private static void OnGeneratedCSProjectFiles ()
		{
			// Force execution of VSCode update
			VSCode.UpdateSolution ();
		}
	}
}