using System;
using NefitEasyDotNet.Models.Internal;

namespace NefitEasyDotNet.Models
{
    public class ProgramSwitch
    {
        public DateTime Timestamp { get; }

        public ProgramName Name { get; }

        public double Temperature { get; }

        internal ProgramSwitch(NefitSwitch prog)
        {
            var now = NefitEasyUtils.GetNextDate(prog.d, prog.t);
            Timestamp = now;
            Temperature = prog.T;
        }

        internal ProgramSwitch(NefitProgram prog)
        {
            var now = NefitEasyUtils.GetNextDate(prog.d, prog.t);
            Timestamp = now;
            Name = (ProgramName) prog.name;
            Temperature = prog.T;            
        }
    }
}