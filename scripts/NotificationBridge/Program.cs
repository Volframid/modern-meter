using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

var path=Path.GetFullPath(args[0]);
using var asm=AssemblyDefinition.ReadAssembly(path,new ReaderParameters{InMemory=true,ReadSymbols=false});
var module=asm.MainModule;var type=module.Types.Single(t=>t.FullName=="DamageMeter.Processing.NotifyProcessor");
if(type.Fields.Any(f=>f.Name=="ModernAbnormalityObserved"))
{
 if(!type.Fields.Any(f=>f.Name=="ModernCreditsObserved") || !type.Methods.Any(m=>m.Name=="RecordModernAbnormalityObserved")
  || !type.Methods.Any(m=>m.Name=="RecordModernCreditsObserved"))throw new InvalidOperationException("Incomplete notification context bridge");
 Console.WriteLine("Notification context bridge already installed.");return;
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
var ab=Bridge("ModernAbnormalityObserved",typeof(Action<object,object,int,int>),new[]{typeof(object),typeof(object),typeof(int),typeof(int)});
var credits=Bridge("ModernCreditsObserved",typeof(Action<object,int>),new[]{typeof(object),typeof(int)});
var abnormal=type.Methods.Single(m=>m.Name=="AbnormalityNotifierCommon");
var credit=type.Methods.Single(m=>m.Name=="UpdateCredits"&&m.Parameters.Count==2);
Instruction AddSite(MethodDefinition m)=>m.Body.Instructions.Single(i=>i.OpCode==OpCodes.Callvirt&&i.Operand is MethodReference f&&f.Name=="Add"&&f.DeclaringType.FullName.Contains("NotifyFlashMessage"));
void Before(MethodDefinition m,Instruction site,params Instruction[] instructions){var il=m.Body.GetILProcessor();foreach(var i in instructions)il.InsertBefore(site,i);m.Body.MaxStackSize=Math.Max(12,m.Body.MaxStackSize+6);}
var add=AddSite(abnormal);var target=abnormal.Parameters[0].ParameterType;
Before(abnormal,add,Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldarg_1));
if(target.IsValueType)Before(abnormal,add,Instruction.Create(OpCodes.Box,target));
Before(abnormal,add,Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Ldarg_3),Instruction.Create(OpCodes.Call,ab));
Before(credit,AddSite(credit),Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,credits));
var margin=credit.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldc_I4&&i.Operand is int n&&n==500);var subtract=margin.Next;
if(subtract.OpCode!=OpCodes.Sub)throw new InvalidOperationException("Unexpected credits threshold IL");
margin.OpCode=OpCodes.Ldc_I4_3;margin.Operand=null;subtract.OpCode=OpCodes.Mul;
var end=subtract.Next;Before(credit,end,Instruction.Create(OpCodes.Ldc_I4_4),Instruction.Create(OpCodes.Div));
var output=path+".patched";asm.Write(output);File.Move(output,path,true);
Console.WriteLine("Installed exact abnormality/credit context bridge and75percent credits threshold.");
