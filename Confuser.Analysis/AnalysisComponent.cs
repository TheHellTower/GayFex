using System.ComponentModel.Composition;
using Confuser.Analysis.Properties;
using Confuser.Analysis.Services;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Analysis {
	[Export(typeof(IConfuserComponent))]
	internal sealed class AnalysisComponent : IConfuserComponent {
		public string Name => Resources.AnalysisComponentName;

		public string Description => Resources.AnalysisComponentDescription;

		public void Initialize(IServiceCollection collection) {
			collection.AddSingleton(p => new AnalysisService(p));
			collection.AddTransient<IAnalysisService>(p => p.GetRequiredService<AnalysisService>());
		}
		public void PopulatePipeline(IProtectionPipeline pipeline) { }
	}
}
