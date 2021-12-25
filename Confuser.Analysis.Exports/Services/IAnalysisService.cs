using dnlib.DotNet;

namespace Confuser.Analysis.Services {
    public interface IAnalysisService
    {
        IVTable GetVTable(ITypeDefOrRef typeDefOrRef);
    }
}
