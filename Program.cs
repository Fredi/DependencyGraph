﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DependencyGraph
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Something went wrong! Please specify the root folder and the output PNG path.");
                Console.WriteLine("Example: dg.exe C:\\Projects\\ProjectFolder yuml.png [-i]");
                Console.WriteLine("Options: -i (include assembly references)");
                return 1;
            }

            var rootFolder = args[0];

            if (!Directory.Exists(rootFolder))
            {
                Console.WriteLine("Root folder does not exist!");
                return 1;
            }

            var includeAssemblyReferences = args.Length == 3 && args[2] == "-i";

            var yuml = GenerateYUML(rootFolder, includeAssemblyReferences);

            try
            {
                using (var webClient = new WebClient())
                {
                    File.WriteAllBytes(args[1], webClient.DownloadData("http://yuml.me/diagram/scruffy/class/" + SafeYUML(yuml)));
                }

                Process process = new Process();
                process.StartInfo.FileName = args[1];
                process.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was an error when writing to the output file: " + ex.Message);
            }

            return 0;
        }

        private static string SafeYUML(string yuml)
        {
            return System.Web.HttpUtility.HtmlEncode(yuml.Replace(Environment.NewLine, ", ")
                                                         .Replace(@"\n", ", ")
                                                         .Replace(",,", ","));
        }

        private static string GenerateYUML(string rootFolder, bool includeAssemblyReferences = false)
        {
            var sb = new StringBuilder();
            var projects = Directory.GetFiles(rootFolder, "*.csproj", SearchOption.AllDirectories);

            foreach (var project in projects)
            {
                var projectName = Path.GetFileNameWithoutExtension(project);

                XElement projectNode = XElement.Load(project);
                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
                var dependencies = projectNode.Descendants(ns + "ItemGroup")
                                              .Descendants(ns + "ProjectReference")
                                              .Descendants()
                                              .Where(d => d.Name == ns + "Name")
                                              .Select(d => d.Value);

                if (includeAssemblyReferences)
                {
                    var assemblyReference = projectNode.Descendants(ns + "ItemGroup")
                                                       .Descendants(ns + "Reference")
                                                       .Select(d => d.Attribute("Include").Value);
                    dependencies = dependencies.Concat(assemblyReference);
                }

                foreach (var dependency in dependencies)
                {
                    sb.Append(string.Format("[{0}] -> [{1}]{2}", projectName, dependency, Environment.NewLine));
                }
            }

            return sb.ToString();
        }
    }
}
