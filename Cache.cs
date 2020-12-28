using Mono.Cecil;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Dragons.HookGenCompatibility
{
    public static class Cache
    {
        public static Dictionary<string, string> transformTypeNameCache = new Dictionary<string, string>();
        
        public static bool IsPublic(TypeDefinition def)
        {
            if (def.IsPublic) return true;
            if (def.IsNestedPublic)
            {
                //TypeReference baseRef = def.BaseType;
                //TypeDefinition baseDef = Patcher.AssemblyCSharp.MainModule.GetType(baseRef.FullName); // XXX small chance of failing, find the proper way to do this
                if (IsPublic(def.DeclaringType)) return true;
            }
            return false;
        }
        
        public static TypeReference FindRelatedPublicType(TypeReference frm)
        {
            TypeDefinition def = Patcher.AssemblyCSharp.MainModule.GetType(frm.FullName);
            
            bool possible = true;
            if (def == null) return null;
            
            if (def.IsEnum && !IsPublic(def))
            {
                foreach (FieldDefinition f in def.Fields)
                {
                    if (f.Name == "value__")
                    {
                        return f.FieldType;
                    }
                }
            }
            while (!IsPublic(def))
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
                Patcher.impossibles++;
                return null;
            }
            Patcher.logger.LogInfo(frm.FullName + " became " + def.FullName);
            return def;
            //logger.LogInfo("parameter " + param.ParameterType.FullName + " became " + def.FullName);
            //param.ParameterType = m.Module.ImportReference(def);
        }
        
        public static string TransformTypeName(string fullName, string declared, string name)
        {
            string res;
            if (transformTypeNameCache.TryGetValue(fullName, out res)) return res;
            
            if ((!name.StartsWith("orig_") && !name.StartsWith("hook_") && !name.StartsWith("add_") && !name.StartsWith("remove_")) || !declared.StartsWith("On."))
            {
                transformTypeNameCache[fullName] = name;
                return name;
            }
            
            TypeDefinition asmtyp = Patcher.FindTypeWithName(Patcher.AssemblyCSharp, declared.Substring(3));
            if (asmtyp == null)
            {
                Patcher.logger.LogWarning("Could not find type for: " + declared.Substring(3));
                return name;
            }
            
            MethodDefinition intendedMethod = null;
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
                string mname = method.Name;
                if (mname.StartsWith("."))
                {
                    mname = mname.Substring(1);
                }
                mname += suffix;
                
                // Patcher.logger.LogInfo("Compare " + mname + "          from             " + method.FullName + "              named             " + method.Name + "      with       " + name.Substring(name.Split('_')[0].Length + 1));
                
                if (mname == name.Substring(name.Split('_')[0].Length + 1))
                {
                    intendedMethod = method;
                }
            }
            if (intendedMethod == null)
            {
                Patcher.logger.LogError("Could not figure out what " + fullName + " is!");
                return name;
            }
            
            string realSuffix = null;
            if (intendedMethod.Parameters.Count == 0) realSuffix = "";
            IEnumerable<MethodDefinition> source = null;
            if (realSuffix == null)
            {
                source = from other in asmtyp.Methods
                    where !other.HasGenericParameters && other.Name == intendedMethod.Name && other != intendedMethod
                    select other;
                if (source.Count() == 0)
                {
                    realSuffix = "";
                }
            }
            if (realSuffix == null)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < intendedMethod.Parameters.Count; i++)
                {
                    ParameterDefinition param = intendedMethod.Parameters[i];
                    string typeName;
                    if (!ReflTypeNameMap.TryGetValue(param.ParameterType.FullName, out typeName))
                    {
                        typeName = GetFriendlyName(param.ParameterType, false);
                    }
                    if (source.Any((MethodDefinition other) => {
                        ParameterDefinition otherParam = other.Parameters.ElementAtOrDefault(i);
                        return otherParam != null && GetFriendlyName(otherParam.ParameterType, false) == typeName && otherParam.ParameterType.Namespace != param.ParameterType.Namespace;
                    }))
                    {
                        typeName = GetFriendlyName(param.ParameterType, true);
                    }
                    sb.Append("_");
                    sb.Append(typeName.Replace(".", "").Replace("`", ""));
                }
                realSuffix = sb.ToString();
            }
            string realName = intendedMethod.Name;
            if (realName.StartsWith(".")) realName = realName.Substring(1);
            realName += realSuffix;
            
            /*
            TODO: handle the case where there are two events with the same name, which leads to an additional suffix.
            This happens a total of... twice.
            Still important though.
            */
            
            string prefix = name.Split('_')[0] + "_";
            string finalResult = prefix + realName;
            
            transformTypeNameCache[fullName] = finalResult;
            return finalResult;
        }
        
        public static string GetFriendlyName(TypeReference typ, bool full)
        {
            if (typ is TypeSpecification)
            {
                return BuildFriendlyName(new StringBuilder(), typ, full).ToString();
            }
            if (full) return typ.FullName;
            return typ.Name;
        }
        
        public static StringBuilder BuildFriendlyName(StringBuilder sb, TypeReference typ, bool full)
        {
            if (!(typ is TypeSpecification))
            {
                sb.Append((full ? typ.FullName : typ.Name).Replace("_", ""));
                return sb;
            }
            if (typ.IsByReference)
            {
                sb.Append("ref");
            }
            else if (typ.IsPointer)
            {
                sb.Append("ptr");
            }
            BuildFriendlyName(sb, (typ as TypeSpecification).ElementType, full);
            if (typ.IsArray)
            {
                sb.Append("Array");
            }
            return sb;
        }
        
		public static readonly Dictionary<string, string> ReflTypeNameMap = new Dictionary<string, string>
		{
			{
				typeof(string).FullName,
				"string"
			},
			{
				typeof(object).FullName,
				"object"
			},
			{
				typeof(bool).FullName,
				"bool"
			},
			{
				typeof(byte).FullName,
				"byte"
			},
			{
				typeof(char).FullName,
				"char"
			},
			{
				typeof(decimal).FullName,
				"decimal"
			},
			{
				typeof(double).FullName,
				"double"
			},
			{
				typeof(short).FullName,
				"short"
			},
			{
				typeof(int).FullName,
				"int"
			},
			{
				typeof(long).FullName,
				"long"
			},
			{
				typeof(sbyte).FullName,
				"sbyte"
			},
			{
				typeof(float).FullName,
				"float"
			},
			{
				typeof(ushort).FullName,
				"ushort"
			},
			{
				typeof(uint).FullName,
				"uint"
			},
			{
				typeof(ulong).FullName,
				"ulong"
			},
			{
				typeof(void).FullName,
				"void"
			}
		};
    }
}