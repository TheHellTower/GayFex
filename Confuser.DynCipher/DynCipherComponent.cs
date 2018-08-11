using System.ComponentModel.Composition;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.DynCipher {
	[Export(typeof(IConfuserComponent))]
	internal sealed class DynCipherComponent : IConfuserComponent {
		public string Name => "Dynamic Cipher";

		public string Description => "Provides dynamic cipher generation services.";

		public IConfuserContext Context { get; set; }

		public void Initialize(IServiceCollection collection) =>
			collection.AddSingleton(typeof(IDynCipherService), new DynCipherService());

		public void PopulatePipeline(IProtectionPipeline pipeline) {
			//
		}
	}
}
