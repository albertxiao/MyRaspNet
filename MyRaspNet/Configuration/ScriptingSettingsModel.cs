using System.Collections.Generic;

namespace MyRaspNet.Configuration
{
    public class ScriptingSettingsModel
    {
        public string ScriptsPath { get; set; }

        public List<string> IncludePaths { get; set; }
    }
}
