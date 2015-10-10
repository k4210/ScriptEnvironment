using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ScriptExecutor
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        private static bool bUsingConsole = true;

        public static void Log(string msg)
        {
            if (bUsingConsole)
            {
                Console.WriteLine(msg);
            }
            else
            {
                //MessageBox.Show(msg, "SctripExecutor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void LogError(string msg)
        {
            if (bUsingConsole)
            {
                Console.Error.WriteLine(msg);
            }
            else
            {
                //MessageBox.Show(msg, "SctripExecutor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void WaitForInput()
        {
            if (bUsingConsole)
            {
                Console.ReadKey();
            }
        }

        public static void HandleError(string msg)
        {
            Log(msg);
            WaitForInput();
            Environment.Exit(-1);
        }

        public static void CloseConsole()
        {
            FreeConsole();
            bUsingConsole = false;
        }

        private static int FillSource(ref string source)
        {
            StringBuilder full_source = new StringBuilder();
            full_source.AppendLine("#pragma warning disable 1633");
            full_source.AppendLine("using System;");
            full_source.AppendLine("using System.Data;");
            full_source.AppendLine("using System.Text;");
            full_source.AppendLine("using System.Windows.Forms;");
            full_source.AppendLine("using System.IO;");
            full_source.AppendLine("namespace LocalScript {");
            full_source.AppendLine("class CSharpScript {");
            full_source.AppendLine("static void Main(string[] args) {");
            full_source.AppendLine(source);
            full_source.AppendLine(" } } }");

            source = full_source.ToString();

            return 9;
        }

        private static bool ExecuteOneLine(string code, string[] args)
        {
            FillSource(ref code);

            CompilerParameters compiler_parameters = new CompilerParameters();
            compiler_parameters.GenerateExecutable = true;
            compiler_parameters.GenerateInMemory = true;
            compiler_parameters.TreatWarningsAsErrors = true;
            compiler_parameters.CompilerOptions = "/nowarn:1633"; // unrecognized pragmas
            compiler_parameters.ReferencedAssemblies.Add("system.dll");
            compiler_parameters.ReferencedAssemblies.Add("system.data.dll");
            compiler_parameters.ReferencedAssemblies.Add("system.windows.forms.dll");

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerResults results = provider.CompileAssemblyFromSource(compiler_parameters, code);
            if (results.Errors.HasErrors)
            {
                StringBuilder error_msg = new StringBuilder();
                foreach (CompilerError err in results.Errors)
                    error_msg.AppendFormat("{0} at column {1} \n", err.ErrorText, err.Column);
                LogError(error_msg.ToString());
                return false;
            }

            MethodInfo method = results.CompiledAssembly.EntryPoint;
            if (null == method)
            {
                LogError("null == method");
                return false;
            }
            try
            {
                string[] str_parameters = new string[args.Length - 1];
                Array.Copy(args, 1, str_parameters, 0, args.Length - 1);
                object[] parameters = new object[1] { str_parameters };
                method.Invoke(null, parameters);
            }
            catch (TargetInvocationException ex)
            {
                LogError("Script error: " + ex.InnerException.Message);
                return false;
            }
            catch (Exception ex)
            {
                LogError("Executor error: " + ex.Message);
                return false;
            }
            return true;
        }

        static void Main(string[] args)
        {
            String s;
            String whole_script = "";
            do
            {
                s = Console.ReadLine();
                if (s.EndsWith(";"))
                {
                    whole_script += s + "\n";
                }
                else
                {
                    string whole_line = "Console.Write(\">> \"); Console.WriteLine(" + s + ");";
                    whole_script += whole_line + "\n";
                    ExecuteOneLine(whole_script, args);
                    whole_script = "";
                    Console.WriteLine();
                }
            } while (!String.IsNullOrEmpty(s));
        }

        static void OldMain(string[] args)
        {
            if (args.Length == 0) HandleError("Please specify a C# script file");
            string path = args[0];
            if (!File.Exists(path)) HandleError("Specified file does not exist");
            bool partial_script = path.EndsWith(".scs");
            bool library_script = path.EndsWith(".dcs");
            string source = File.ReadAllText(path);
            int first_line = 0;
            if (partial_script)
            {
                first_line = FillSource(ref source);
            }

            CompilerParameters compiler_parameters = new CompilerParameters();
            compiler_parameters.GenerateExecutable = !library_script;
            compiler_parameters.GenerateInMemory = true;
            compiler_parameters.TreatWarningsAsErrors = true;
            compiler_parameters.CompilerOptions = "/nowarn:1633"; // unrecognized pragmas
            compiler_parameters.ReferencedAssemblies.Add("system.dll");
            compiler_parameters.ReferencedAssemblies.Add("system.data.dll");
            compiler_parameters.ReferencedAssemblies.Add("system.windows.forms.dll");
            //compiler_parameters.ReferencedAssemblies.Add("ScriptCommon.dll");

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerResults results = provider.CompileAssemblyFromSource(compiler_parameters, source);
            if (results.Errors.HasErrors)
            {
                StringBuilder error_msg = new StringBuilder();
                foreach (CompilerError err in results.Errors)
                    error_msg.AppendFormat("{0} at line {1} column {2} \n", err.ErrorText, err.Line - first_line, err.Column);
                HandleError(error_msg.ToString());
            }

            try
            {
                Object called_obj = null;
                MethodInfo method = null;
                object[] parameters = null;
                if (library_script)
                {
                    called_obj = results.CompiledAssembly.CreateInstance("LocalScript.Script");
                    if (null == called_obj) HandleError("null == CreateInstance(\"LocalScript.Script\")");
                    method = results.CompiledAssembly.GetType("LocalScript.Script").GetMethod("Execute");
                    //parameters = new object[1] { new ExecutorCallbackImpl() };
                }
                else
                {
                    method = results.CompiledAssembly.EntryPoint;
                    string[] str_parameters = new string[args.Length - 1];
                    Array.Copy(args, 1, str_parameters, 0, args.Length - 1);
                    parameters = new object[1] { str_parameters };
                }
                if (null == method) HandleError("null == method");

                method.Invoke(called_obj, parameters);
            }
            catch (TargetInvocationException ex)
            {
                HandleError("Script error: " + ex.InnerException.Message);
            }
            catch (Exception ex)
            {
                HandleError("Executor error: " + ex.Message);
            }
        }
    }
}
