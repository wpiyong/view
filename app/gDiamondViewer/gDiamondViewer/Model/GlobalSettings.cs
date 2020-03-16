using SettingsLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gDiamondViewer.Model
{
    public class GlobalSettings : Settings
    {
        public GlobalSettings()
            : base("globalSettings.config")
        {
        }

        public int Version { get; set; }
    }
}
