using System;
using System.IO;
using BepInEx.Logging;
using Mono.Cecil;
// using MonoMod;

/*
namespace Dragons.HookGenCompatibility
{
    public class HookgenMonoModder : MonoModder
    {
        public ManualLogSource logger;
        
        public HookgenMonoModder(AssemblyDefinition asm, ManualLogSource logger)
        {
            this.Module = asm.MainModule;
            this.logger = logger;
        }
        
		public override void Log(object value)
		{
			this.Logger.LogMessage(value);
		}
        
		public override void Log(string value)
		{
			this.Logger.LogMessage(value);
		}
        
		public override void LogVerbose(object value)
		{
			if (!this.LogVerboseEnabled)
			{
				return;
			}
			this.Logger.LogDebug(value);
		}
        
		public override void LogVerbose(string value)
		{
			if (!this.LogVerboseEnabled)
			{
				return;
			}
			this.Logger.LogDebug(value);
		}
        
        public override void Dispose()
        {
            this.Module = null;
            base.Dispose();
        }
        
        public void DoPatch()
        {
            this.Read();
            this.MapDependencies();
            this.PatchRefs();
            this.Log("[Main] AutoPatch");
            this.AutoPatch();
            this.Log("[Main] Done.");
        }
        
        public override void PostRelinker(IMetadataTokenProvider mtp, IGenericParameterProvider context)
        {
            if (mtp is TypeReference || mtp is MethodReference)
            {
                MemberReference obj = mtp as MemberReference;
                string newName = Cache.TransformTypeName(obj.FullName, obj.DeclaringType.FullName, obj.Name);
                obj.Name = newName;
                return obj;
            }
            return mtp;
        }
    }
}
*/