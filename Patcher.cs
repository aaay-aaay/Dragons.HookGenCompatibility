using BepInEx;
using Mono.Cecil;
using System.IO;
using BepInEx.Logging;
using System.Collections.Generic;
using Mono.Cecil.Cil;

// TODO: Use MonoMod relinker!
// use PostRelinker

namespace Dragons.HookGenCompatibility
{
    public static class Patcher
    {
        public static void Patch(AssemblyDefinition assembly)
        {
            foreach (ModuleDefinition module in assembly.Modules)
            {
                PatchModule(module);
            }
        }
        
        public static void PatchModule(ModuleDefinition module)
        {
            if (module == null)
            {
                logger.LogWarning("Module is null??");
                return;
            }
            
            if (!VerifyIsPartiality(module)) return;
            if (!VerifyUsesHookGen(module)) return;
            
            AssemblyCSharp = AssemblyDefinition.ReadAssembly(Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll"));
            
            /*
            foreach (TypeDefinition typ in module.GetTypes())
            {
                foreach (MethodDefinition m in typ.Methods)
                {
                    MethodBody b = m.Body;
                    if (b == null) continue;
                    foreach (Instruction inst in b.Instructions)
                    {
                        if (inst.OpCode == OpCodes.Ldftn && inst.Next != null && inst.Next.OpCode == OpCodes.Newobj && inst.Next.Next != null && inst.Next.Next.OpCode == OpCodes.Call)
                        {
                            MethodReference loadedMethod = (MethodReference)inst.Operand;
                            MethodReference eventAdd = (MethodReference)inst.Next.Next.Operand;
                            if (!eventAdd.Name.StartsWith("add_")) continue;
                            logger.LogInfo(m.FullName + " hooks " + eventAdd.FullName + " with " + loadedMethod.FullName);
                            
                            string name = eventAdd.DeclaringType.FullName.Substring(3);
                            TypeDefinition asmtyp = FindTypeWithName(AssemblyCSharp, name);
                            logger.LogInfo("Assembly-CSharp type: " + asmtyp.FullName);
                            
                            foreach (MethodDefinition method in asmtyp.Methods)
                            {
                                int index = (from other in asmtyp.Methods
                                where !other.HasGenericParameters && other.Name == method.Name
                                select other).ToList().IndexOf(method);
                                string suffix = "";
                                if (index != 0)
                                {
                                    suffix = index.ToString();
                                    do
                                    {
                                        suffix = "_" + suffix;
                                    } while (asmtyp.Methods.Any((MethodDefinition other) => !other.HasGenericParameters && other.Name == method.Name + suffix));
                                }
                                string name = method.Name;
                                if (name.StartsWith("."))
                                {
                                    name = name.Substring(1);
                                }
                                name += suffix;
                                
                                
                            }
                        }
                    }
                }
            }
            */
            
            foreach (TypeDefinition typ in module.GetTypes())
            {
                TransformTypeDef(typ);
            }
            
            if (impossibles > 0)
            {
                logger.LogWarning("Failed to resolve " + impossibles + " parameters");
            }
        }
        
        public static void TransformTypeDef(TypeDefinition typ)
        {
            foreach (MethodDefinition method in typ.Methods)
            {
                TransformMethodDef(method);
            }
            foreach (FieldDefinition field in typ.Fields)
            {
                TransformFieldDef(field);
            }
        }
        
        public static void TransformMethodDef(MethodDefinition method)
        {
            TransformMethodRef(method);
            if (!method.HasBody) return;
            foreach (Instruction inst in method.Body.Instructions)
            {
                if (inst.Operand is TypeReference) TransformTypeRef(inst.Operand as TypeReference);
                if (inst.Operand is MethodReference) TransformMethodRef(inst.Operand as MethodReference);
            }
        }
        
        public static void TransformFieldDef(FieldDefinition field)
        {
            TransformTypeRef(field.FieldType);
        }
        
        public static void TransformTypeRef(TypeReference typ)
        {
            if (typ == null) return;
            if (typ.DeclaringType == null) return;
            // logger.LogInfo("Transform type: " + typ.FullName);
            string newName = Cache.TransformTypeName(typ.FullName, typ.DeclaringType.FullName, typ.Name);
            if (typ.Name != newName)
            {
                typ.Name = newName;
            }
        }
        
        public static void TransformMethodRef(MethodReference m)
        {
            foreach (ParameterDefinition param in m.Parameters)
            {
                TransformTypeRef(param.ParameterType);
            }
            TransformTypeRef(m.DeclaringType);
            
            // logger.LogInfo("Transform method: " + m.FullName + " - " + m.DeclaringType.FullName + " - " + m.Name);
            
            string newName = Cache.TransformTypeName(m.FullName, m.DeclaringType.FullName, m.Name);
            if (m.Name != newName)
            {
                m.Name = Cache.TransformTypeName(m.FullName, m.DeclaringType.FullName, m.Name);
            }
            
            if (m.Name == "Invoke")
            {
                if (m.DeclaringType.FullName.StartsWith("On."))
                {
                    logger.LogInfo(m.FullName);
                    TypeReference tr = null;
                    foreach (ParameterDefinition param in m.Parameters)
                    {
                        // TypeDefinition def = AssemblyCSharp.MainModule.LookupToken(param.ParameterType.MetadataToken) as TypeDefinition;
                        /*
                        TypeDefinition def = AssemblyCSharp.MainModule.GetType(param.ParameterType.FullName);
                        bool possible = true;
                        if (def == null) continue;
                        while (!def.IsPublic && !def.IsNestedPublic)
                        {
                            try
                            {
                                def = def.BaseType.Resolve();
                            }
                            catch
                            {
                                possible = false;
                                break;
                            }
                        }
                        if (!possible)
                        {
                            impossibles++;
                            continue;
                        }
                        logger.LogInfo("parameter " + param.ParameterType.FullName + " became " + def.FullName);
                        param.ParameterType = m.Module.ImportReference(def);
                        */
                        tr = Cache.FindRelatedPublicType(param.ParameterType);
                        if (tr == null) continue;
                        param.ParameterType = m.Module.ImportReference(tr);
                    }
                    tr = Cache.FindRelatedPublicType(m.ReturnType);
                    if (tr != null)
                        m.ReturnType = m.Module.ImportReference(tr);
                }
            }
        }
        
        public static TypeDefinition FindTypeWithName(AssemblyDefinition asm, string name)
        {
            foreach (ModuleDefinition module in asm.Modules)
            {
                TypeDefinition res = module.GetType(name);
                if (res != null) return res;
            }
            return null;
        }
        
        public static bool VerifyIsPartiality(ModuleDefinition module)
        {
            foreach (TypeDefinition typ in module.GetTypes())
            {
                if (typ.BaseType == null)
                {
                    continue;
                }
                if (typ.BaseType.FullName == "Partiality.Modloader.PartialityMod")
                {
                    // pretty safe to say that that means it's a partiality mod
                    // in reality it's not going to happen unless someone tries to trick Dragons.HookGenCompatibility intentionally
                    return true;
                }
            }
            // if it doesn't have a PartialityMod in it, it's probably not a partiality mod
            return false;
        }
        
        public static bool VerifyUsesHookGen(ModuleDefinition module)
        {
            foreach (AssemblyNameReference reference in module.AssemblyReferences)
            {
                if (reference.FullName == "HOOKS-Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                {
                    return true; // that's obviously hookgen
                }
            }
            return false; // if this returns false negatives, investigate further
        }
        
        public static IEnumerable<string> FindTargetDLLs()
        {
            IEnumerable<string> dlls = Directory.GetFiles(Path.Combine(Paths.BepInExRootPath, "plugins"), "*.dll");
            List<string> result = new List<string>();
            foreach (string dll in dlls)
            {
                string dllName = Path.GetFileName(dll);
                result.Add(dllName);
            }
            return result;
        }
        
        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                return FindTargetDLLs();
            }
        }
        
        public static ManualLogSource logger = Logger.CreateLogSource("Dragons.HookGenCompatibility");
        
        public static AssemblyDefinition AssemblyCSharp;
        
        public static int impossibles = 0;
        
        public static string updateURL = "http://beestuff.pythonanywhere.com/audb/api/mods/0/18";
        public static int version = 0;
        public static string keyE = "AQAB";
        public static string keyN = "yu7XMmICrzuavyZRGWoknFIbJX4N4zh3mFPOyfzmQkil2axVIyWx5ogCdQ3OTdSZ0xpQ3yiZ7zqbguLu+UWZMfLOBKQZOs52A9OyzeYm7iMALmcLWo6OdndcMc1Uc4ZdVtK1CRoPeUVUhdBfk2xwjx+CvZUlQZ26N1MZVV0nq54IOEJzC9qQnVNgeeHxO1lRUTdg5ZyYb7I2BhHfpDWyTvUp6d5m6+HPKoalC4OZSfmIjRAi5UVDXNRWn05zeT+3BJ2GbKttwvoEa6zrkVuFfOOe9eOAWO3thXmq9vJLeF36xCYbUJMkGR2M5kDySfvoC7pzbzyZ204rXYpxxXyWPP5CaaZFP93iprZXlSO3XfIWwws+R1QHB6bv5chKxTZmy/Imo4M3kNLo5B2NR/ZPWbJqjew3ytj0A+2j/RVwV9CIwPlN4P50uwFm+Mr0OF2GZ6vU0s/WM7rE78+8Wwbgcw6rTReKhVezkCCtOdPkBIOYv3qmLK2S71NPN2ulhMHD9oj4t0uidgz8pNGtmygHAm45m2zeJOhs5Q/YDsTv5P7xD19yfVcn5uHpSzRIJwH5/DU1+aiSAIRMpwhF4XTUw73+pBujdghZdbdqe2CL1juw7XCa+XfJNtsUYrg+jPaCEUsbMuNxdFbvS0Jleiu3C8KPNKDQaZ7QQMnEJXeusdU=";
    }
}