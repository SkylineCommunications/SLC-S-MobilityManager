using System;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.Messages.Advanced;

namespace AddNewLocation
{
    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
    public class Script
    {
        /// <summary>
        /// The script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(IEngine engine)
        {
            try
            {
                RunSafe(engine);
            }
            catch (ScriptAbortException)
            {
                // Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
                throw; // Comment if it should be treated as a normal exit of the script.
            }
            catch (ScriptForceAbortException)
            {
                // Catch forced abort exceptions, caused via external maintenance messages.
                throw;
            }
            catch (ScriptTimeoutException)
            {
                // Catch timeout exceptions for when a script has been running for too long.
                throw;
            }
            catch (InteractiveUserDetachedException)
            {
                // Catch a user detaching from the interactive script by closing the window.
                // Only applicable for interactive scripts, can be removed for non-interactive scripts.
                throw;
            }
            catch (Exception e)
            {
                engine.ExitFail("Run|Something went wrong: " + e);
            }
        }

        private void RunSafe(IEngine engine)
        {
            string rawName = engine.GetScriptParam("Name").Value;
            string rawAddress = engine.GetScriptParam("Address").Value;
            string name = NormalizeString(rawName);
            string address = NormalizeString(rawAddress);

            SetDataMinerInfoMessage setDataMinerInfo = new SetDataMinerInfoMessage
            {
                bInfo1 = 0,
                bInfo2 = 0,
                DataMinerID = 1007918,
                ElementID = -1,
                HostingDataMinerID = 1007918,
                IInfo1 = 0,
                IInfo2 = 0,
                Sa2 = new SA
                {
                    Sa = new[]
                    {
                    String.Empty,  // Generated GUID
                    "1",    // Selected option [Add Event=1]
                    name,    // Name
                    address,   // Address
                    },
                },
                Uia1 = new UIA
                {
                    Uia = new uint[]
                    {
                    1007918,    // DataMiner ID
                    490,  // Element ID
                    10700,    // Events Context Menu Parameter ID
                    },
                },
                What = (int)NotifyType.NT_ALL_TRAP_INFO,
            };

            engine.SendSLNetSingleResponseMessage(setDataMinerInfo);
        }

        private string NormalizeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            s = s.Trim(); // whitespace

            // strip outer [ ] if present
            if (s.Length >= 2 && s.StartsWith("[") && s.EndsWith("]"))
                s = s.Substring(1, s.Length - 2);

            // strip surrounding quotes
            s = s.Trim('\"');

            return s.Trim(); // final whitespace clean
        }
    }
}
