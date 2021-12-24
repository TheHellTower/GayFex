using System.Reflection;
using Newtonsoft.Json;

namespace NewtonsoftJsonSerialization {
	[Obfuscation(Exclude = true)]
	internal class ObfExcluded {
		[JsonProperty("a")] public string a;

		[JsonProperty("b")] public string b;

		[JsonProperty("c")] public string c;

		public ObfExcluded(string a, string b, string c) {
			this.a = a;
			this.b = b;
			this.c = c;
		}

		public override string ToString() => JsonConvert.SerializeObject(this);
	}
}
