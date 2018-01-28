using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;

namespace ExampleAppendLine
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Welcome();
                Go(args);
            }
            catch (Exception x)
            {
                if (!string.IsNullOrEmpty(x.Message))
                {
                    Console.WriteLine("Exception: {0}", x.Message);
                }
            }
            Console.ReadLine();
        }

        private static void Welcome()
        {
            Console.WriteLine("This is a test project for SoftLock file locking.");
            Console.WriteLine("During execution text line will be appended several times to a file.");
            Console.WriteLine("Use different tokens and lengths and check resulting file.");
            Console.WriteLine("This is test project for SoftLock file locking.");
        }

        private static Random randomObject = new Random();

        private static void Go(string[] args)
        {
            List<string> argsList = new List<string>(args);

            string inputFileName = Take(argsList, "FILE.txt");
            string inputLineLength = Take(argsList, "100");
            string inputLineToken = Take(argsList, new string((char)((byte)'A' + randomObject.Next(26)), 1));
            string inputTestDelay = Take(argsList, "100");
            string inputTestCount = Take(argsList, "10");
            string inputUseLock = Take(argsList, "Y");
            string inputUseFileStream = Take(argsList, "Y");

            if (NotSufficientNumberOfArguments)
            {
                inputFileName = Ask("File name to write", inputFileName);
                inputLineLength = Ask("Line length", inputLineLength);
                inputLineToken = Ask("Line token", inputLineToken);
                inputTestDelay = Ask("Delay between writes", inputTestDelay);
                inputTestCount = Ask("Number of tests", inputTestCount);
                inputUseLock = Ask("Use locking", inputUseLock);
                inputUseFileStream = Ask("Use FileStream instead of File.AppendAllText", inputUseFileStream);
            }

            int lineLength = 0;
            if (!int.TryParse(inputLineLength, out lineLength) || lineLength < 1)
                throw new Exception("Line length must be a positive number");

            if (string.IsNullOrEmpty(inputLineToken))
                throw new Exception("Line token must not be empty");
            
            int testCount = 0;
            if (!int.TryParse(inputTestCount, out testCount) || testCount < 1)
                throw new Exception("Number of tests must be a positive number");

            int testDelay = 0;
            int.TryParse(inputTestDelay, out testDelay);

            bool useLock = inputUseLock.ToUpper() == "Y" || inputUseLock.ToUpper() == "YES";
            bool useFileStream = inputUseFileStream.ToUpper() == "Y" || inputUseFileStream.ToUpper() == "YES";

            StringBuilder sampleBuilder = new StringBuilder(lineLength);
            while (sampleBuilder.Length < lineLength)
                sampleBuilder.Append(inputLineToken);
            sampleBuilder.Remove(lineLength, sampleBuilder.Length - lineLength);
            sampleBuilder.AppendLine();
            string sample = sampleBuilder.ToString();

            Console.WriteLine();
            Console.WriteLine("During the test, sample will be written to a file.");
            Console.WriteLine("If any exception occurs, it will be written out to console.");
            Console.WriteLine("Sample text to use:");
            Console.WriteLine(sample);
            Console.WriteLine();

            Console.WriteLine("Enter anything to begin test...");
            Console.ReadLine();

            byte[] byteBuffer = null;
            
            if (useFileStream)
                byteBuffer = System.Text.Encoding.UTF8.GetBytes(sample);

            string fileName = inputFileName;

            FileLock.FileLock softLock = null;
            if (useLock)
                softLock = new FileLock.FileLock(fileName + ".lck");

            Console.WriteLine("{0} {1}", DateTime.Now.ToString("hh:MM:ss.fff")
                , "Test started");

            for (int i = 0; i < testCount; i++)
            {
                bool lockAcquired = false;
                try
                {
                    if (softLock != null)
                    {
                        if (!softLock.Acquire())
                        {
                            Console.WriteLine("{0} {1}", DateTime.Now.ToString("hh:MM:ss.fff")
                                , "Lock not acquired");
                            continue;
                        }
                        else
                        {
                            lockAcquired = true;
                        }
                    }
                    if (useFileStream)
                    {
                        using (FileStream fs = new FileStream(inputFileName
                            , FileMode.Append, FileAccess.Write, FileShare.None))
                        {
                            fs.Write(byteBuffer, 0, byteBuffer.Length);
                            fs.Flush();
                            fs.Close();
                        }
                        Thread.Sleep(0);
                    }
                    else
                    {
                        File.AppendAllText(inputFileName, sample);
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine("{0} {1}", DateTime.Now.ToString("hh:MM:ss.fff")
                        , x.Message);
                }
                finally
                {
                    if (softLock != null)
                    {
                        if (!softLock.Release())
                        {
                            if (lockAcquired)
                            {
                                Console.WriteLine("{0} {1}", DateTime.Now.ToString("hh:MM:ss.fff")
                                    , "Lock release error");
                            }
                        }
                    }                        
                    if (testDelay > 0)
                        Thread.Sleep(testDelay);
                }
            }

            Console.WriteLine("Test finished, enter anything to end program.");
        }

        private static string Ask(string message, string defaultValue)
        {
            Console.Write(message);
            if (!string.IsNullOrEmpty(defaultValue))
                Console.Write(" [" + defaultValue + "]");
            Console.Write(": ");
            string input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
                input = defaultValue;
            return input;
        }

        private static bool NotSufficientNumberOfArguments = false;

        private static string Take(List<string> list, string defaultValue)
        {
            if (list.Count == 0)
            {
                NotSufficientNumberOfArguments = true;
                return defaultValue;
            }
            string value = list[0];
            list.RemoveAt(0);
            return value;
        }

        public static string inputTestCount { get; set; }

        public static string inputTestDelay { get; set; }
    }
}
