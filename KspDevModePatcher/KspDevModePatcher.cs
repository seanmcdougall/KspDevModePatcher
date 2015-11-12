// See http://forum.kerbalspaceprogram.com/threads/114241-KSP-Plugin-debugging-for-Visual-Studio-and-Monodevelop-on-all-OS for details
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;

namespace KspDevModePatcher
{
    class KspDevModePatcher
    {
        private readonly Log log = new Log(typeof(KspDevModePatcher).Name);

        public void Run()
        {
            try
            {
                var filename = "Assembly-CSharp.dll";
                if (!File.Exists(filename))
                {
                    log.Error("File {0} not found! Please run me in KSP_DATA\\Managed", filename);
                    return;
                }
                var asm = AssemblyDefinition.ReadAssembly(filename);


                MoveInitializerIntoAwake(asm, "AtmosphereFromGround", "\u0003", 8, "Start");
                MoveInitializerIntoAwake(asm, "AtmosphereFromGround", "\u0004", 8, "Start");
                MoveInitializerIntoAwake(asm, "AtmosphereFromGround", "\u0005", 8, "Start");
                MoveInitializerIntoAwake(asm, "AtmosphereFromGround", "\u0006", 8, "Start");
                MoveInitializerIntoAwake(asm, "AtmosphereFromGround", "\a", 8, "Start");
                MoveInitializerIntoAwake(asm, "AtmosphereFromGround", "\b", 8, "Start");
                MoveInitializerIntoAwake(asm, "AtmosphereFromGround", "\t", 8, "Start");

                MoveInitializerIntoAwake(asm, "FlightIntegrator", "sunLayerMask", 24, "Start");

                MoveInitializerIntoAwake(asm, "GameSettings", "INPUT_DEVICES", 2, "Awake");

                MoveInitializerIntoAwake(asm, "HighLogic", "\u0019", 8, "Awake");
                MoveInitializerIntoAwake(asm, "HighLogic", "\u001A", 8, "Awake");
                MoveInitializerIntoAwake(asm, "HighLogic", "\u001B", 8, "Awake");
                MoveInitializerIntoAwake(asm, "HighLogic", "\u001C", 8, "Awake");
                MoveInitializerIntoAwake(asm, "HighLogic", "\u001D", 8, "Awake");

                MoveInitializerIntoAwake(asm, "MapView", "\r", 8, "Start");

                MoveInitializerIntoAwake(asm, "ModuleAblator", "\b\b", 8, "Start");

                MoveInitializerIntoAwake(asm, "PhysicsGlobals", "\u001d\u0002", 8, "Awake");

                MoveInitializerIntoAwake(asm, "PQSMod_MaterialQuadRelative", "\u001e\u0002", 8, "Awake");
                MoveInitializerIntoAwake(asm, "PQSMod_MaterialQuadRelative", "\u001f\u0002", 8, "Awake");
                MoveInitializerIntoAwake(asm, "PQSMod_MaterialQuadRelative", " \u0002", 8, "Awake");

                MoveInitializerIntoAwake(asm, "PQSMod_OceanFX", "\n\u0003", 8, "Awake");
                MoveInitializerIntoAwake(asm, "PQSMod_OceanFX", "\v\u0003", 8, "Awake");
                MoveInitializerIntoAwake(asm, "PQSMod_OceanFX", "\f\u0003", 8, "Awake");
                MoveInitializerIntoAwake(asm, "PQSMod_OceanFX", "\r\u0003", 8, "Awake");

                MoveInitializerIntoAwake(asm, "SkySphereControl", "\u0002", 8, "Start");
                MoveInitializerIntoAwake(asm, "SkySphereControl", "\u0003", 8, "Start");
                MoveInitializerIntoAwake(asm, "SkySphereControl", "\u0004", 8, "Start");

                MoveInitializerIntoAwake(asm, "SunShaderController", "\b", 8, "Start");
                MoveInitializerIntoAwake(asm, "SunShaderController", "\t", 8, "Start");
                MoveInitializerIntoAwake(asm, "SunShaderController", "\n", 8, "Start");
                MoveInitializerIntoAwake(asm, "SunShaderController", "\v", 8, "Start");

                MoveInitializerIntoAwake(asm, "UnderwaterTint", "colorID", 8, "Awake");

                /*var m = asm.MainModule.GetType("GameSettings").Methods.First(x => x.Name == "Awake");
                log.Debug("{0}", m);
                foreach(var instr in m.Body.Instructions) {
                    log.Debug("{0}", instr);
                }*/

                log.Debug("Writing file {0}", filename);
                asm.Write(filename);

                log.Info("File patched. Happy debugging!");
            }
            catch (Exception e)
            {
                log.Error("Exception while trying to patch assembly: {0}", e.Message);
            }
        }

        private void MoveInitializerIntoAwake(AssemblyDefinition asmdef, string typeName, string fieldName, int numberOfInstructionsToMove, string destMethod)
        {
            var type = asmdef.MainModule.GetType(typeName);
            var field = type.Fields.First(x => x.Name == fieldName);
            var initializerFn = type.Methods.First(x => x.Name == ".cctor");

            log.Debug("Looking for stsfld instruction for {0} in {1}", EncodeNonAsciiCharacters(field.ToString()), initializerFn);
            List<Instruction> toMove = new List<Instruction>();
            foreach (var instr in initializerFn.Body.Instructions)
            {
                if (instr.OpCode == OpCodes.Stsfld && instr.Operand == field)
                {
                    log.Debug("Found instruction at offset {0}: {1}", instr.Offset, EncodeNonAsciiCharacters(instr.ToString()));
                    var candidate = instr;
                    while (toMove.Count < numberOfInstructionsToMove)
                    {
                        toMove.Add(candidate);
                        candidate = candidate.Previous;
                    }
                }
            }
            if (!toMove.Any())
            {
                log.Error(String.Format("No initializer instruction found for {0}. Is the file already patched?", field));
                return;
            }

            toMove.ForEach(x => initializerFn.Body.Instructions.Remove(x));

            // Reverse them because we added them while going backwards.
            // So we can just insert them in order.
            toMove.Reverse();

            var awakeFn = type.Methods.FirstOrDefault(x => x.Name == destMethod);
            if (awakeFn == null)
            {
                log.Debug("{0} has no Awake method, creating one.", type);
                // TODO: Make method virtual and call super?
                // But none of the types we are currently creating methods for have super Awake() methods, so meh.
                awakeFn = new MethodDefinition("Awake", MethodAttributes.Public, asmdef.MainModule.Import(typeof(void)));
                awakeFn.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                type.Methods.Add(awakeFn);

            }

            foreach (var it in toMove.Select((x, i) => new { Value = x, Index = i }))
            {
                awakeFn.Body.Instructions.Insert(it.Index, it.Value);
            }

            log.Debug("AFTER PATCHING {0} {1} - - - - - - - - - - - - - - - - - - -", type, awakeFn);
            foreach (var instr in awakeFn.Body.Instructions.Take(numberOfInstructionsToMove + 10))
            {
                log.Debug("{0}", EncodeNonAsciiCharacters(instr.ToString()));
            }
        }

        static void Main(string[] args)
        {
            new KspDevModePatcher().Run();
        }

        // http://stackoverflow.com/a/1615860
        private static string EncodeNonAsciiCharacters(string value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in value)
            {
                if (c < 32 || c > 127)
                {
                    string encodedValue = "\\u" + ((int)c).ToString("x4");
                    sb.Append(encodedValue);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }

    public class Log
    {
        private static readonly string ns = typeof(Log).Namespace;
        private readonly string id = String.Format("{0:X8}", Guid.NewGuid().GetHashCode());
        private readonly string name;

        public Log(string name)
        {
            this.name = name;
        }

        private void Print(string level, string message, params object[] values)
        {
            Console.WriteLine("[" + name + ":" + level + ":" + id + "]  " + String.Format(message, values));
        }

        public void Debug(string message, params object[] values)
        {
            Print("DEBUG", message, values);
        }

        public void Info(string message, params object[] values)
        {
            Print("INFO", message, values);
        }

        public void Error(string message, params object[] values)
        {
            Print("ERROR", message, values);
        }
    }


}
