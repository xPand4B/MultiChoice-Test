﻿using Server.Enums;
using Server.File;
using System;
using System.Collections.Generic;

namespace Server.Logging
{
    internal class Log : FileOperation, IDisposable
    {
        private string _path = "log";
        public virtual string Path { get => _path; protected set => _path = value; }
        public bool ConsoleOutput { get; set; }

        /// <summary>
        /// Get Log
        /// </summary>
        public List<string> GetLog()
        {
            ReadLinesFromFile(_path);
            return Lines;
        }

        /// <summary>
        /// Append text to Log.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="type"></param>
        public virtual void AppendToLog(string text, LogType type)
        {
            var timestamp = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss");
            var log = type.ToString() + ";" + timestamp + ";" + text;
            if (ConsoleOutput)
                Console.WriteLine(type.ToString() + ": " + text + " at " + timestamp);
            WriteLineToFile(Path, log);
        }

        /// <summary>
        ///
        /// </summary>
        public virtual void Dispose()
        {
            _path = null;
            Path = null;
        }
    }
}