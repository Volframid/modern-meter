using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

var path=Path.GetFullPath(args[0]);
using var asm=AssemblyDefinition.ReadAssembly(path,new ReaderParameters{InMemory=true,ReadSymbols=false});
var module=asm.MainModule;var type=module.Types.Single(t=>t.FullName=="DamageMeter.Processing.NotifyProcessor");
var abnormal = type.Methods.Single(m => m.Name == "AbnormalityNotifierCommon");
var credit = type.Methods.Single(m => m.Name == "UpdateCredits" && m.Parameters.Count == 2);
var contextInstalled = type.Fields.Any(f => f.Name == "ModernAbnormalityObserved");
if (contextInstalled &&
    (!type.Fields.Any(f => f.Name == "ModernCreditsObserved") ||
     !type.Methods.Any(m => m.Name == "RecordModernAbnormalityObserved") ||
     !type.Methods.Any(m => m.Name == "RecordModernCreditsObserved")))
    throw new InvalidOperationException("Incomplete notification context bridge");

const string thresholdFieldName = "ModernCreditsThreshold";
const string thresholdMethodName = "GetModernCreditsThreshold";
var thresholdField = type.Fields.SingleOrDefault(f => f.Name == thresholdFieldName);
var thresholdMethod = type.Methods.SingleOrDefault(m => m.Name == thresholdMethodName);
var thresholdCall = credit.Body.Instructions.SingleOrDefault(i => i.OpCode == OpCodes.Call &&
    i.Operand is MethodReference m && m.Name == thresholdMethodName && m.DeclaringType.FullName == type.FullName);
if (thresholdField != null || thresholdMethod != null || thresholdCall != null)
{
    if (!contextInstalled || thresholdField == null || thresholdMethod == null ||
        thresholdCall?.Previous.OpCode != OpCodes.Div ||
        thresholdCall.Next?.OpCode != OpCodes.Blt_S)
        throw new InvalidOperationException("Incomplete credits threshold bridge");

    Console.WriteLine("Notification context and credits threshold bridge already installed.");
    return;
}

MethodDefinition Bridge(string name,Type callback,Type[] parameters)
{
 var field=new FieldDefinition(name,FieldAttributes.Public|FieldAttributes.Static,module.ImportReference(callback));type.Fields.Add(field);
 var method=new MethodDefinition("Record"+name,MethodAttributes.Private|MethodAttributes.Static,module.TypeSystem.Void);
 foreach(var p in parameters)method.Parameters.Add(new ParameterDefinition(module.ImportReference(p)));type.Methods.Add(method);
 var il=method.Body.GetILProcessor();var has=Instruction.Create(OpCodes.Nop);
 il.Emit(OpCodes.Ldsfld,field);il.Emit(OpCodes.Dup);il.Emit(OpCodes.Brtrue_S,has);il.Emit(OpCodes.Pop);il.Emit(OpCodes.Ret);il.Append(has);
 for(int j=0;j<parameters.Length;j++)il.Emit(OpCodes.Ldarg,method.Parameters[j]);
 il.Emit(OpCodes.Callvirt,module.ImportReference(callback.GetMethod("Invoke")));il.Emit(OpCodes.Ret);return method;
}
Instruction AddSite(MethodDefinition m)=>m.Body.Instructions.Single(i=>i.OpCode==OpCodes.Callvirt&&i.Operand is MethodReference f&&f.Name=="Add"&&f.DeclaringType.FullName.Contains("NotifyFlashMessage"));
void Before(MethodDefinition m,Instruction site,params Instruction[] instructions){var il=m.Body.GetILProcessor();foreach(var i in instructions)il.InsertBefore(site,i);m.Body.MaxStackSize=Math.Max(12,m.Body.MaxStackSize+6);}

var creditIl = credit.Body.GetILProcessor();
if (!contextInstalled)
{
    var ab=Bridge("ModernAbnormalityObserved",typeof(Action<object,object,int,int>),new[]{typeof(object),typeof(object),typeof(int),typeof(int)});
    var credits=Bridge("ModernCreditsObserved",typeof(Action<object,int>),new[]{typeof(object),typeof(int)});
    var add=AddSite(abnormal);var target=abnormal.Parameters[0].ParameterType;
    Before(abnormal,add,Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldarg_1));
    if(target.IsValueType)Before(abnormal,add,Instruction.Create(OpCodes.Box,target));
    Before(abnormal,add,Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Ldarg_3),Instruction.Create(OpCodes.Call,ab));
    Before(credit,AddSite(credit),Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,credits));

    var margin=credit.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldc_I4&&i.Operand is int n&&n==500);
    var subtract=margin.Next;
    if(subtract.OpCode!=OpCodes.Sub)throw new InvalidOperationException("Unexpected credits threshold IL");
    margin.OpCode=OpCodes.Ldc_I4_3;margin.Operand=null;subtract.OpCode=OpCodes.Mul;
    Before(credit,subtract.Next,Instruction.Create(OpCodes.Ldc_I4_4),Instruction.Create(OpCodes.Div));
}

var division = credit.Body.Instructions.Single(i => i.OpCode == OpCodes.Div &&
    i.Previous?.OpCode == OpCodes.Ldc_I4_4 &&
    i.Previous.Previous?.OpCode == OpCodes.Mul &&
    i.Previous.Previous.Previous?.OpCode == OpCodes.Ldc_I4_3 &&
    i.Next?.OpCode == OpCodes.Blt_S);
var selectorType = typeof(Func<int, int>);
thresholdField = new FieldDefinition(thresholdFieldName, FieldAttributes.Public | FieldAttributes.Static,
    module.ImportReference(selectorType));
type.Fields.Add(thresholdField);
thresholdMethod = new MethodDefinition(thresholdMethodName, MethodAttributes.Private | MethodAttributes.Static,
    module.TypeSystem.Int32);
thresholdMethod.Parameters.Add(new ParameterDefinition("fallback", ParameterAttributes.None, module.TypeSystem.Int32));
type.Methods.Add(thresholdMethod);
var selectorIl = thresholdMethod.Body.GetILProcessor();
var useSelector = Instruction.Create(OpCodes.Ldarg_0);
selectorIl.Emit(OpCodes.Ldsfld, thresholdField);
selectorIl.Emit(OpCodes.Dup);
selectorIl.Emit(OpCodes.Brtrue_S, useSelector);
selectorIl.Emit(OpCodes.Pop);
selectorIl.Emit(OpCodes.Ldarg_0);
selectorIl.Emit(OpCodes.Ret);
selectorIl.Append(useSelector);
selectorIl.Emit(OpCodes.Callvirt, module.ImportReference(selectorType.GetMethod("Invoke")));
selectorIl.Emit(OpCodes.Ret);
creditIl.InsertAfter(division, Instruction.Create(OpCodes.Call, thresholdMethod));

var output=path+".patched";asm.Write(output);File.Move(output,path,true);
Console.WriteLine("Installed notification context and credits threshold bridge.");
