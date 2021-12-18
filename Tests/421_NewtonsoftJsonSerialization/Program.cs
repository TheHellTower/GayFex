using System;

namespace NewtonsoftJsonSerialization {
	class Program {
		static int Main(string[] args) {
			Console.WriteLine("START");

			Console.WriteLine(new ObfMarkedWithAttribute("a", "b", "c").ToString());
			Console.WriteLine(new ObfExcluded("a", "b", "c").ToString());

			Console.WriteLine("END");

			return 42;
		}
	}
}
