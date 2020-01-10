using System;

using SmartQuant;

namespace OpenQuant
{
	class Program
	{
		static void Main(string[] args)
		{
			Scenario scenario = new PaperIB(Framework.Current);

			scenario.Run();
		}
	}
}
