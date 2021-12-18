using Newtonsoft.Json;

namespace NewtonsoftJsonSerialization {
	[JsonObject]
	internal class ObfMarkedWithAttribute {
		[JsonProperty("a")] public string a;

		[JsonProperty("b")] public string b;

		[JsonProperty("c")] public string c;

		public ObfMarkedWithAttribute(string a, string b, string c) {
			this.a = a;
			this.b = b;
			this.c = c;
		}

		public override string ToString() => JsonConvert.SerializeObject(this);
	}
}
