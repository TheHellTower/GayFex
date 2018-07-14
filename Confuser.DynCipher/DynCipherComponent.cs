using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.DynCipher {
	[Export(typeof(IConfuserComponent))]
	internal sealed class DynCipherComponent : IConfuserComponent {
		public const string _ServiceId = "Confuser.DynCipher";

		public string Name => "Dynamic Cipher";

		public string Description => "Provides dynamic cipher generation services.";

		public string Id => _ServiceId;

		public string FullId => _ServiceId;

		public IConfuserContext Context { get; set; }

		public void Initialize(IServiceCollection collection) =>
			collection.AddSingleton(typeof(IDynCipherService), new DynCipherService());

		public void PopulatePipeline(IProtectionPipeline pipeline) {
			//
		}
	}
}
