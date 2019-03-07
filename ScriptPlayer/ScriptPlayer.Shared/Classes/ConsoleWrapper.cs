using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ScriptPlayer.Shared
{
    public class ConsoleWrapper
    {
        private readonly ProcessStartInfo _startInfo;
        private StreamWriter _input;

        protected string File
        {
            get  => _startInfo.FileName;
            set => _startInfo.FileName = value;
        }
        protected string Arguments
        {
            get => _startInfo.Arguments;
            set => _startInfo.Arguments = value;
        }

        public ConsoleWrapper(string file, string arguments) : this()
        {
            _startInfo.FileName = file;
            _startInfo.Arguments = arguments;
        }

        protected ConsoleWrapper()
        {
            _startInfo = new ProcessStartInfo();
            _startInfo.UseShellExecute = false;
            _startInfo.CreateNoWindow = true;
            _startInfo.RedirectStandardError = true;
            _startInfo.RedirectStandardInput = true;
            _startInfo.RedirectStandardOutput = true;
        }

        protected virtual void BeforeExecute() { }
        protected virtual void AfterExecute() { }

        public void Execute()
        {
            BeforeExecute();

            Process process = Process.Start(_startInfo);

            _input = process.StandardInput;

            Thread outputThread = new Thread(ReadOutput);
            outputThread.Start(new Tuple<StreamReader, bool>(process.StandardOutput, false));

            Thread errorThread = new Thread(ReadOutput);
            errorThread.Start(new Tuple<StreamReader, bool>(process.StandardError, true));

            process.WaitForExit();

            Debug.WriteLine("EXITCODE = " + process.ExitCode);

            _input = null;

            outputThread.Join();
            errorThread.Join();

            AfterExecute();
        }

        protected void Input(string text, bool enter = true)
        {
            if(enter)
                _input?.WriteLine(text);
            else
                _input?.Write(text);
        }

        private void ReadOutput(object args)
        {
            Tuple<StreamReader, bool> arguments = (Tuple<StreamReader,bool>)args;

            while (!arguments.Item1.EndOfStream)
            {
                ProcessLine(arguments.Item1.ReadLine(), arguments.Item2);
            }
        }

        protected virtual void ProcessLine(string line, bool isError)
        {
            Debug.WriteLine((isError ? "ERR: " : "OUT: ") + line);
        }
    }
}