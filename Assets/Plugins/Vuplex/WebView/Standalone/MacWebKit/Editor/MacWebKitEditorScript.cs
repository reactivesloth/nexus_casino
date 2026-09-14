// Copyright (c) 2026 Vuplex Inc. All rights reserved.
//
// Licensed under the Vuplex Commercial Software Library License, you may
// not use this file except in compliance with the License. You may obtain
// a copy of the License at
//
//     https://vuplex.com/commercial-library-license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#if UNITY_EDITOR_OSX
using System.Diagnostics;
using UnityEditor;

namespace Vuplex.WebView.Editor {

    /// <summary>
    /// Mac editor script that modifies the VuplexWebViewMacWebKit.bundle
    /// native plugin downloaded from the Asset Store to allow it to run.
    /// </summary>
    [InitializeOnLoad]
    class MacWebKitEditorScript {

        static MacWebKitEditorScript() {

            // For details on why this is necessary, see the comments in MacEditorScript.cs.
            var pluginPath = EditorUtils.FindDirectory("Assets/Vuplex/WebView/Standalone/MacWebKit/Plugins/VuplexWebViewMacWebKit.bundle");
            _executeBashCommand("xattr -d com.apple.quarantine \"" + pluginPath + "\"");
        }

        static string _executeBashCommand(string command) {

            // Escape quotes
            command = command.Replace("\"","\"\"");
            var proc = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "/bin/bash",
                    Arguments = "-c \""+ command + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            proc.WaitForExit();
            return proc.StandardOutput.ReadToEnd();
        }
    }
}
#endif
