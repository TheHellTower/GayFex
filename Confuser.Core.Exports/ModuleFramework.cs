namespace Confuser.Core {
	public enum ModuleFramework {
		/// <summary>Failed to identify the type of framework used by the assembly.</summary>
		Unknown,

		/// <summary>Any variant of the .NET Framework is used. Spanning all versions up to 4.8.</summary>
		DotNetFramework,

		/// <summary>Any variant of .NET Core starting from 1.0 up to 3.1; also .NET 5 or 6.</summary>
		DotNet,

		/// <summary>Any version of the .NET Standard 1.0 up to 2.1 environment.</summary>
		DotNetStandard,

		/// <summary>Universal windows platform</summary>
		Uwp
	}
}
