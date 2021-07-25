namespace Confuser.Renamer {
	public struct DisplayNormalizedName {
		public string DisplayName { get; }

		public string NormalizedName { get; }

		public DisplayNormalizedName(string displayName, string normalizedName) {
			DisplayName = displayName;
			NormalizedName = normalizedName;
		}
	}
}
