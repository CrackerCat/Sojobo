﻿using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ES.Sojobo;
using static ES.Sojobo.Model;
using ES.Kpot2StringExtractor.ExtensionMethods;
using System.Runtime.InteropServices;

namespace ES.Kpot2StringExtractor
{
    public class Program
    {
        private static Int32 _decryptFunctionEndAddress = 0x0040C928;        

        private static Tuple<String, Boolean, Boolean> GetOptions(String[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage {0} <file name>", Path.GetFileName(Assembly.GetEntryAssembly().Location));
                Console.WriteLine("Pass '--test' as first argument to run the sample released with this program.");
                Console.WriteLine("Pass '--strings' as first argument to dump all strings in the sample released with this program.");
                Environment.Exit(0);
            }

            var emulateSample = args[0].Equals("--test", StringComparison.OrdinalIgnoreCase);
            var printallStringsFromSample = args[0].Equals("--strings", StringComparison.OrdinalIgnoreCase);
            return Tuple.Create<String, Boolean, Boolean>(args[0], emulateSample, printallStringsFromSample);
        }

        private static Byte[] Decrypt(String content)
        {
            var decodedContent = Convert.FromBase64String(content);
            var decryptedContent = decodedContent.Select(b => (Byte)(b ^ 0xAA)).ToArray();
            decryptedContent[0] = (Byte)'M';
            decryptedContent[1] = (Byte)'Z';
            return decryptedContent;
        }

        private static Byte[] GetSampleContent()
        {
            /*                 
            This sample is taken from article: https://www.proofpoint.com/us/threat-insight/post/new-kpot-v20-stealer-brings-zero-persistence-and-memory-features-silently-steal
            The binary is "obfuscated" with XOR, removed PE signature and base64 encoded.
            SHA256: 67f8302a2fd28d15f62d6d20d748bfe350334e5353cbdef112bd1f8231b5599d
            */
            Console.WriteLine(@"
I'm going to run a sample of Kpot from article: https://www.proofpoint.com/us/threat-insight/post/new-kpot-v20-stealer-brings-zero-persistence-and-memory-features-silently-steal
The binary is 'obfuscated' with XOR, removed PE signature and base64 encoded.
Original SHA256: 67f8302a2fd28d15f62d6d20d748bfe350334e5353cbdef112bd1f8231b5599d
Do you want to continue (It should be pretty safe to run this test) ? [Y/N]
");
            if (Console.ReadLine().Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                var testFile = "KPot2_REAL_MALWARE_DO_NOT_RUN_IT.txt";
                Console.WriteLine("[+] Running test sample: {0}", testFile);
                var assemblyDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var content = File.ReadAllText(Path.Combine(assemblyDir, testFile));
                return Decrypt(content);
            }
            else
            {
                Console.WriteLine("Test aborted");
                Environment.Exit(1);
                return null;
            }
        }

        private static Byte[] GetFileContent(String filename)
        {
            return File.ReadAllBytes(filename);
        }
        
        private static ISandbox CreateSandbox(Byte[] content)
        {
            var sandbox = new Win32Sandbox();
            sandbox.Load(content);

            // this will allows to invoke our functions
            sandbox.AddLibrary(typeof(Program).Assembly);

            return sandbox;
        }

        private static void ProcessStep(Object sender, IProcessContainer process)
        {
            var ip = process.GetProgramCounter().ToInt32();
            if (ip == _decryptFunctionEndAddress)
            {
                // read registers value
                var decryptedBufferAddress = process.GetRegister("EDI").ToUInt64();
                var bufferLength = process.GetRegister("EAX").ToInt32();
                
                // read decrypted string
                var decryptedBuffer = process.Memory.ReadMemory(decryptedBufferAddress, bufferLength);
                var decryptedString = Encoding.UTF8.GetString(decryptedBuffer);
                Console.WriteLine("[+] {0}", decryptedString);
            }
        }

        private static void EnableStepping(ISandbox sandbox)
        {
            var process = sandbox.GetRunningProcess();
            process.Step += ProcessStep;
        }

        private static void Run(ISandbox sandbox)
        {
            try
            {
                Console.WriteLine("-=[ Start Emulation ]=-");
                sandbox.Run();
            }
            catch {
                /* Exception due to some limitation in this emulator */
                Console.WriteLine("-=[ Emulation Completed ]=-");
            }
        }

        private static void DecryptStrings(IProcessContainer process)
        {
            Console.WriteLine("-=[ Start Dump All Strings ]=-");
            
            // encrypted strings
            var encryptedStringsStartAddress = 0x00401288UL;
            var encryptedStringsEndAddress = 0x00401838UL;

            var currentOffset = encryptedStringsStartAddress;
            while (currentOffset < encryptedStringsEndAddress)
            {
                var encryptedString = process.Memory.ReadMemory<EncryptedString>(currentOffset);
                var decryptedString = encryptedString.Decrypt(process);
                Console.WriteLine("[+] {0}", decryptedString);

                // go to the next strings
                currentOffset += 8UL; 
            }

            Console.WriteLine("-=[ Dump All Strings Completed ]=-");
        }

        static void Main(string[] args)
        {
            var (filename, emulateSample, printAllStrings) = GetOptions(args);
            if (printAllStrings)
            {
                // print all strings
                var content = GetSampleContent();
                var sandbox = CreateSandbox(content);
                var process = sandbox.GetRunningProcess();
                DecryptStrings(process);
            }
            else
            {
                // emulate content
                var content = emulateSample ? GetSampleContent() : GetFileContent(filename);
                var sandbox = CreateSandbox(content);
                EnableStepping(sandbox);


                sandbox.AddLibrary(@"C:\Windows\SysWOW64\kernel32.dll");


                Run(sandbox);
            }            
        }
    }
}
