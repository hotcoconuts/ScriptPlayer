using System;
using System.Collections.Generic;
using System.Linq;

namespace ScriptPlayer.Shared.Scripts
{
    public static class VorzeToFunscriptConverter
    {
        public static List<FunScriptAction> Convert(List<VorzeScriptAction> actions)
        {
            actions = actions.OrderBy(a => a.TimeStamp).ToList();

            List<VorzeScriptAction> filteredActions = new List<VorzeScriptAction>();

            foreach (VorzeScriptAction action in actions)
            {
                if (filteredActions.Count == 0)
                    filteredActions.Add(action);
                else if (filteredActions.Last().TimeStamp >= action.TimeStamp)
                    continue;
                else
                    filteredActions.Add(action);
            }

            actions = filteredActions;

            List<FunScriptAction> funActions = new List<FunScriptAction>();

            int lastSpeed = 0;
            int lastPosition = 50;
            TimeSpan lastTimeStamp = actions[0].TimeStamp;
            int newPosition;
            for (int i = 0; i < actions.Count; i++)
            {
                var dur = (actions[i].TimeStamp - lastTimeStamp).TotalMilliseconds;
                //var speedMapped = lastSpeed * 0.6 + 20;
                double absDelta = Math.Pow(lastSpeed * 100 / 25000.0, 1 / 1.05) * dur;
                var delta = (actions[i].Action == 0 ? absDelta : -absDelta);
                //  double speed = 25000 * Math.Pow(delta / info.Duration.TotalMilliseconds, 1.05) / 100.0; --->
                // Action stores direction, Parameter stores speed
                // currently, position is simply clamped, so after going out of range the Launch will stay still in situations where the Vorze would be spinning
                newPosition = (int)Math.Round(lastPosition + delta);
                //newPosition = Math.Max(5, Math.Min(95, newPosition)); //clamping disabled as a test
                
                funActions.Add(new FunScriptAction
                {
                    Position = newPosition,
                    TimeStamp = actions[i].TimeStamp
                });

                lastPosition = newPosition;
                lastSpeed = actions[i].Parameter;
                lastTimeStamp = actions[i].TimeStamp;
            }

            return funActions;
        }
    }
}