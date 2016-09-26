using Logging;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagingProcessorWorker
{
    class EventProcessorFactory : IEventProcessorFactory
    {
        private string logfile;

        public EventProcessorFactory(string logfileName)
        {
            logfile = logfileName;
        }

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            var processor = new EventProcessor();
            processor.ProcessorClosed += this.ProcessorOnProcessorClosed;
            processor.LogMessage += ProcessorOnLogMessage;
            return processor;
        }

        private void ProcessorOnLogMessage(object sender, EventArgs e)
        {
            if(e is LogMessageEventArgs)
            {
                LogMessageEventArgs logMessageEventArgs = e as LogMessageEventArgs;
                Logfile.Get(logfile).Textout(logMessageEventArgs.Color, logMessageEventArgs.List, logMessageEventArgs.Message);
                Logfile.Get(logfile).Update();
            }
        }

        private void ProcessorOnProcessorClosed(object sender, EventArgs eventArgs)
        {
            var processor = sender as EventProcessor;
            if (processor != null)
            {
                // TODO
            }
        }
    }
}
