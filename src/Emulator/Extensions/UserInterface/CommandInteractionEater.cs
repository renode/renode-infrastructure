//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using AntShell.Commands;
using System.Text;
using System.IO;

namespace Antmicro.Renode.UserInterface
{
    public class CommandInteractionWrapper : ICommandInteraction
    {
        public CommandInteractionWrapper(ICommandInteraction commandInteraction)
        {
            underlyingCommandInteraction = commandInteraction;
            data = new StringBuilder();
            error = new StringBuilder();
        }

        public void Clear()
        {
            data.Clear();
            error.Clear();
        }

        public string GetContents()
        {
            return data.ToString();
        }

        public string GetError()
        {
            return error.ToString();
        }

        public Stream GetRawInputStream()
        {
            return underlyingCommandInteraction.GetRawInputStream();
        }

        public string ReadLine()
        {
            return underlyingCommandInteraction.ReadLine();
        }

        public void Write(char c, ConsoleColor? color)
        {
            data.Append(c);
            underlyingCommandInteraction.Write(c, color);
        }

        public void WriteError(string msg)
        {
            error.Append(msg);
            underlyingCommandInteraction.WriteError(msg);
        }

        public string CommandToExecute { get { return underlyingCommandInteraction.CommandToExecute; } set { underlyingCommandInteraction.CommandToExecute = value; } }
        public bool QuitEnvironment { get { return underlyingCommandInteraction.QuitEnvironment; } set { underlyingCommandInteraction.QuitEnvironment = value; } }
        public ICommandInteraction UnderlyingCommandInteraction { get { return underlyingCommandInteraction; } }

        private readonly ICommandInteraction underlyingCommandInteraction;
        private readonly StringBuilder data;
        private readonly StringBuilder error;
    }

    public class CommandInteractionEater : ICommandInteraction
    {
        public void Clear()
        {
            data.Clear();
            error.Clear();
        }

        public void Write(char c, ConsoleColor? color = null)
        {
            data.Append(c);
        }

        public void WriteError(string error)
        {
            this.error.AppendLine(error);
        }

        public string ReadLine()
        {
            return String.Empty;
        }

        public string CommandToExecute { get; set; }

        public bool QuitEnvironment { get; set; }

        public bool HasError => error.Length > 0;

        public string GetContents()
        {
            return data.ToString();
        }

        public string GetError()
        {
            return error.ToString();
        }

        public Stream GetRawInputStream()
        {
            return null;
        }

        private readonly StringBuilder data = new StringBuilder();
        private readonly StringBuilder error = new StringBuilder();
    }

    public class DummyCommandInteraction : ICommandInteraction
    {
        public DummyCommandInteraction(bool verbose = false)
        {
            this.verbose = verbose;
        }

        public string ReadLine()
        {
            return String.Empty;
        }

        public void Write(char c, ConsoleColor? color = default(ConsoleColor?))
        {
            if(verbose)
            {
                Console.Write(c);
            }
        }

        public void WriteError(string error)
        {
            ErrorDetected = true;
            if(verbose)
            {
                Console.WriteLine("ERROR: " + error);
            }
        }

        public Stream GetRawInputStream()
        {
            return null;
        }

        public bool ErrorDetected 
        { 
            get
            {
                var result = errorDetected;
                errorDetected = false;
                return result;
            }

            private set
            {
                errorDetected = value;
            }
        }

        public string CommandToExecute { get; set; }

        public bool QuitEnvironment { get; set; }

        private bool verbose;
        private bool errorDetected;
    }
}

