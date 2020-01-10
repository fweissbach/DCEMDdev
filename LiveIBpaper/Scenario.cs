using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SmartQuant;

namespace OpenQuant
{
    public partial class LiveIBpaper : Scenario
    {
         public LiveIBpaper(Framework framework)
            : base(framework)
        {
		}

        public override void Run()
        {
			Initialize();
        }
    }
}