/* This file is automatically generated. Do not modify it manually! All changes should be made in `RegisterEnumParserContent.tt` file. */
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Antmicro.Renode.CoresSourceParser
{
    public class RegistersEnumParser
    {
        public RegistersEnumParser(Stream stream) : this(stream, new string[0])
        {
        }

        public RegistersEnumParser(Stream stream, IEnumerable<string> defines)
        {
            registers = new List<RegisterDescriptor>();
            registerGroups = new List<RegisterGroupDescriptor>();
            groupedRegisters = new Dictionary<string, List<Tuple<RegisterDescriptor, int>>>();
            handlers = new Dictionary<Mode, Action<string>>
            {
                { Mode.ScanForEnum,          ScanForEnumHandler          },
                { Mode.InsideEnum,           InsideEnumHandler           },
                { Mode.SkipLines,            SkipLinesHandler            },
                { Mode.IncludeLines,         IncludeLinesHandler         }
            };
            modes = new Stack<Mode>();

            this.defines = defines;
            Parse(stream);
        }

        public void Map(string from, string to)
        {
            var regTo = registers.SingleOrDefault(x => x.Name == to);
            if(regTo.Name == null)
            {
                throw new ArgumentException(string.Format("No register named {0} found.", to));
            }

            var regFrom = new RegisterDescriptor
            {
                Name = from,
                Width = regTo.Width,
                Value = regTo.Value
            };

            registers.Add(regFrom);
        }

        public void Ignore(string name)
        {
            var reg = registers.Cast<RegisterDescriptorBase>().Union(registerGroups.Cast<RegisterDescriptorBase>()).SingleOrDefault(x => x.Name == name);
            if(reg != null)
            {
                reg.IsIgnored = true;
            }
        }

        public RegisterDescriptor[] Registers { get { return registers.ToArray(); } }
        public RegisterGroupDescriptor[] RegisterGroups { get { return registerGroups.ToArray(); } }

        private void Parse(Stream stream)
        {
            modes.Push(Mode.ScanForEnum);

            using(var reader = new StreamReader(stream))
            {
                string line;
                while((line = reader.ReadLine()) != null)
                {
                    handlers[modes.Peek()](line);
                }
            }

            foreach(var group in groupedRegisters)
            {
                var widths = group.Value.Select(x => x.Item1.Width).Distinct().ToList();
                if(widths.Count != 1)
                {
                    // we found at least two registers having index with the same name, but different width
                    throw new ArgumentException(string.Format("Inconsistent register width detected for group: {0}", group.Key));
                }

                var groupDescriptor = new RegisterGroupDescriptor
                {
                    Name = group.Key,
                    Width = widths.First(),
                    IndexValueMap = new Dictionary<int, int>()
                };

                foreach(var x in group.Value.Select(x => Tuple.Create(x.Item2, x.Item1.Value)))
                {
                    groupDescriptor.IndexValueMap.Add(x.Item1, x.Item2);
                }

                registerGroups.Add(groupDescriptor);
            }
        }

        private void ScanForEnumHandler(string line)
        {
            if(line == BeginningOfEnum)
            {
                modes.Push(Mode.InsideEnum);
            }
        }

        private void InsideEnumHandler(string line)
        {
            // Trim lines with single line comment only
            line = Regex.Replace(line, @"^(\s*//.*)$", "").Trim();
            if(line.Length == 0)
            {
                return;
            }

            Mode? newMode = null;
            if(line.StartsWith(BeginningOfIfdef, StringComparison.CurrentCulture))
            {
                var value = line.Replace(BeginningOfIfdef, string.Empty).Trim();
                newMode = defines.Contains(value) ? Mode.IncludeLines : Mode.SkipLines;
            }
            else if(line.StartsWith(BeginningOfIfndef, StringComparison.CurrentCulture))
            {
                var value = line.Replace(BeginningOfIfndef, string.Empty).Trim();
                // Notice the modes are inverted compared to 'ifdef'.
                newMode = defines.Contains(value) ? Mode.SkipLines : Mode.IncludeLines;
            }

            if(newMode.HasValue)
            {
                modes.Push(newMode.Value);
                return;
            }

            if(line == EndOfEnum)
            {
                modes.Pop();
                return;
            }

            // e.g., R_0_32 = 147,
            // X_32 = 155,
            var match = Regex.Match(line, @"^(?<name>[\p{L}0-9]+)(_(?<index>[0-9]+))?_(?<width>[0-9]+)\s*=\s*(?<value>((0x)?[0-9a-fA-F]+)|([0-9]+))\s*,?$");
            if(string.IsNullOrEmpty(match.Groups["name"].Value))
            {
                throw new ArgumentException($"Register name was in a wrong format: {line}");
            }

            var regValue = match.Groups["value"].Value;
            var regDesc = new RegisterDescriptor
            {
                Name = match.Groups["name"].Value,
                Width = int.Parse(match.Groups["width"].Value),
                Value = Convert.ToInt32(regValue, regValue.StartsWith("0x") ? 16 : 10)
            };

            if(!string.IsNullOrEmpty(match.Groups["index"].Value))
            {
                if(!groupedRegisters.ContainsKey(regDesc.Name))
                {
                    groupedRegisters[regDesc.Name] = new List<Tuple<RegisterDescriptor, int>>();
                }

                var index = int.Parse(match.Groups["index"].Value);
                groupedRegisters[regDesc.Name].Add(Tuple.Create(regDesc, index));
            }
            else
            {
                registers.Add(regDesc);
            }
        }

        private void IncludeLinesHandler(string line)
        {
            if(line.Trim() == EndOfIfdef)
            {
                modes.Pop();
            }
            else if(line.Trim() == ElseIfdef)
            {
                modes.Pop();
                modes.Push(Mode.SkipLines);
            }
            else
            {
                InsideEnumHandler(line);
            }
        }

        private void SkipLinesHandler(string line)
        {
            if(line.Trim() == EndOfIfdef)
            {
                modes.Pop();
            }
            else if(line.Trim() == ElseIfdef)
            {
                modes.Pop();
                modes.Push(Mode.IncludeLines);
            }
            else if(line.Trim().StartsWith("#if"))
            {
                // The mode should still be 'SkipLines' after ifdef + endif pairs.
                modes.Push(Mode.SkipLines);
            }
        }

        private readonly List<RegisterDescriptor> registers;
        private readonly List<RegisterGroupDescriptor> registerGroups;

        private readonly IEnumerable<string> defines;

        private readonly Dictionary<Mode, Action<string>> handlers;
        private readonly Stack<Mode> modes;
        private readonly Dictionary<string, List<Tuple<RegisterDescriptor, int>>> groupedRegisters;

        private const string BeginningOfIfdef = "#ifdef";
        private const string BeginningOfIfndef = "#ifndef";
        private const string ElseIfdef = "#else";
        private const string EndOfIfdef = "#endif";
        private const string BeginningOfEnum = "typedef enum {";
        private const string EndOfEnum = "} Registers;";

        private enum Mode
        {
            ScanForEnum,
            InsideEnum,
            SkipLines,
            IncludeLines
        }

        public class RegisterDescriptor : RegisterDescriptorBase
        {
            public int Value { get; set; }
        }

        public class RegisterGroupDescriptor : RegisterDescriptorBase
        {
            public Dictionary<int, int> IndexValueMap { get; set; }

            public IEnumerable<RegisterDescriptor> GetRegisters()
            {
                return IndexValueMap.Select(x => new RegisterDescriptor
                {
                    Name = $"{this.Name}{x.Key}",
                    Width = this.Width,
                    IsIgnored = this.IsIgnored,
                    Value = x.Value
                });
            }
        }

        public class RegisterDescriptorBase
        {
            public string Name { get; set; }
            public int Width { get; set; }
            public bool IsIgnored { get; set; }
        }
    }

    public static class RegisterTypeHelper
    {
        public static string GetTypeName(int width)
        {
            switch(width)
            {
            case 64:
                return "UInt64";
            case 32:
                return "UInt32";
            case 16:
                return "UInt16";
            case 8:
                return "byte";
            default:
                throw new ArgumentException("Width not supported");
            }
        }
    }
}

