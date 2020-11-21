using Kourin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestScript
{
    class Program
    {
        static string MyPath;

        static void Main(string[] args) {
            MyPath = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            MyPath += "/../..";

            All();
        }

        static void All() {
            foreach (var i in Directory.GetFiles($"{MyPath}/in")) {
                One(Path.GetFileNameWithoutExtension(i));
                Console.WriteLine();
            }
        }

        static void One(string name) {
            var inp = $"{MyPath}/in/{name}.txt";
            var outp = $"{MyPath}/out/{name}.txt";

            if (File.Exists(outp)) {
                Exec(Read(inp), Read(outp));
            } else {
                ExecNg(Read(inp));
            }
            Console.WriteLine($"[{name}]");
        }

        static string Read(string path) {
            using (var ins = new StreamReader(path)) {
                return ins.ReadToEnd();
            }
        }

        static void Exec(string code, string ret) {
            KourinEngine eng = new KourinEngine();

            Stopwatch sw = new Stopwatch();
            sw.Start();

            string ans;
            bool ok;

            try {
                ans = "" + eng.execute(code);
                ok = ans == ret;
            }catch(Exception ex) {
                ans = ex.Message;
                if(ex is KourinException) {
                    ans += "\r\n" + (ex as KourinException).ScriptStackTrace;
                }
                ok = false;
            }

            sw.Stop();
            Console.WriteLine($"{(ok?"OK":"NG")} : {ans} : {sw.ElapsedMilliseconds}ms");
        }

        static void ExecNg(string code) {
            KourinEngine eng = new KourinEngine();
            
            string ans = "";
            bool ok = false;

            try {
                eng.execute(code);
            } catch(KourinException ex) {
                ans = ex.Message;
                ok = true;
            } catch (Exception ex) {
                ans = ex.Message;
            }

            Console.WriteLine($"{(ok ? "OK" : "NG")} : {ans}");
        }
    }
}
