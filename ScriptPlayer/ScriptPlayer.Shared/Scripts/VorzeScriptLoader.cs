using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptPlayer.Shared.Scripts
{
    public class VorzeScriptToFunscriptLoader : VorzeScriptLoader
    {
        public override List<ScriptAction> Load(Stream stream)
        {
            List<VorzeScriptAction> vorzeActions = base.Load(stream).Cast<VorzeScriptAction>().ToList();
            return VorzeToFunscriptConverter.Convert(vorzeActions).Cast<ScriptAction>().ToList();
        }
    }

    enum AfestaFixerState
    {
        FirstTimestamp,
        Direction,
        SpeedAndNextTime
    }

    public class VorzeScriptLoader : ScriptLoader
    {
        public override List<ScriptAction> Load(Stream stream)
        {
            List<ScriptAction> actions = new List<ScriptAction>();

            using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.ASCII))
            {
                List<string> lines = new List<string>();

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    line = line.Replace("\0", "");
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    lines.Add(line);
                }

                if (lines.Count > 1) {
                    foreach (string line in lines)
                    {
                        int[] parameters = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                        if (parameters.Length < 3)
                            continue;

                        actions.Add(new VorzeScriptAction
                        {
                            TimeStamp = TimeSpan.FromMilliseconds(100.0 * parameters[0]),
                            Action = parameters[1],
                            Parameter = parameters[2]
                        });
                    }
                }

                else if (lines.Count == 1)
                {
                    AfestaFixerState state = AfestaFixerState.FirstTimestamp;
                    int timestamp = -1, direction = -1, speed = -1;

                    // Algorithm for guessing where the linebreaks should be on converted afesta CSVs
                    StringReader file = new StringReader(lines[0]);
                    StringBuilder sb = new StringBuilder();
                    while (file.Peek() != -1)
                    {
                        char c = Convert.ToChar(file.Read());
                        if (c != ',')
                            sb.Append(c);
                        else
                        {
                            switch (state)
                            {
                                case AfestaFixerState.FirstTimestamp:
                                    timestamp = int.Parse(sb.ToString());
                                    sb.Clear();
                                    state = AfestaFixerState.Direction;
                                    break;
                                case AfestaFixerState.Direction:
                                    direction = int.Parse(sb.ToString());
                                    sb.Clear();
                                    state = AfestaFixerState.SpeedAndNextTime;
                                    break;
                                case AfestaFixerState.SpeedAndNextTime:
                                    string block = sb.ToString();
                                    sb.Clear();

                                    int linebreakPosition = block.Length - 1;
                                    int testSpeed = 200, testNextTime = -1;
                                    while ((testNextTime <= timestamp || testSpeed > 100) && linebreakPosition >= 0)
                                    {
                                        testSpeed = int.Parse(block.Substring(0, linebreakPosition));
                                        testNextTime = int.Parse(block.Substring(linebreakPosition));
                                        linebreakPosition--;
                                    }

                                    // Once the split is at a point where the next timestamp comes after the previous one, we have our values
                                    speed = testSpeed;
                                    actions.Add(new VorzeScriptAction
                                    {
                                        TimeStamp = TimeSpan.FromMilliseconds(100.0 * timestamp),
                                        Action = direction,
                                        Parameter = speed
                                    });
                                    timestamp = testNextTime;
                                    state = AfestaFixerState.Direction;
                                    break;
                            }
                        }
                    }

                    if (sb.Length > 0)
                    {
                        // Process the final block
                        switch (state)
                        {
                            case AfestaFixerState.FirstTimestamp:
                                timestamp = int.Parse(sb.ToString());
                                sb.Clear();
                                state = AfestaFixerState.Direction;
                                break;
                            case AfestaFixerState.Direction:
                                direction = int.Parse(sb.ToString());
                                sb.Clear();
                                state = AfestaFixerState.SpeedAndNextTime;
                                break;
                            case AfestaFixerState.SpeedAndNextTime:
                                speed = int.Parse(sb.ToString());
                                sb.Clear();

                                actions.Add(new VorzeScriptAction
                                {
                                    TimeStamp = TimeSpan.FromMilliseconds(100.0 * timestamp),
                                    Action = direction,
                                    Parameter = speed
                                });

                                state = AfestaFixerState.FirstTimestamp;
                                break;
                        }
                    }
                }
            }

            return actions;
        }

        public override List<ScriptFileFormat> GetSupportedFormats()
        {
            return new List<ScriptFileFormat>
            {
                new ScriptFileFormat("Vorze Script (beta)", "csv")
            };
        }
    }
}