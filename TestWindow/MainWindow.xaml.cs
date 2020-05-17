using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Kourin;

namespace TestWindow
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        KourinEngine engine = new KourinEngine();

        public MainWindow()
        {
            InitializeComponent();
            newEngine();
        }

        private void newEngine()
        {
            engine = new KourinEngine();

            //利用者による関数の登録
            engine.setFunction(new KFunc_Hello());

            //DLLからプラグインの登録
            var MyPath = System.IO.Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            foreach (var path in System.IO.Directory.GetFiles(MyPath + "/Plugin")) {
                engine.loadPluginDll(path);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                //for (int i = 0; i < 100000; i++)
                //{
                //  engine.execute(X_Text.Text);
                //}
                sw.Stop();
                X_Block.Text = "" + engine.execute(X_Text.Text);
                X_Time.Text = sw.ElapsedMilliseconds.ToString();
            }
            catch (KourinException ex)
            {
                X_Block.Text = ex.Message + Environment.NewLine + ex.ScriptStackTrace;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            newEngine();
        }

        private class KFunc_Hello : IKourinFunction
        {
            public string name { get { return "HELLO"; } }

            public object execute(object[] args)
            {
                return "Hello!";
            }
        }
    }
}
