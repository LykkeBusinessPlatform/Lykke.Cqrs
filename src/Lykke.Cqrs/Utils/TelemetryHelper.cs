using System;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Lykke.Cqrs.Utils
{
    internal static class TelemetryHelper
    {
        private static readonly TelemetryClient Telemetry = new TelemetryClient();

        internal static IOperationHolder<DependencyTelemetry> InitTelemetryOperation(
            string type,
            string target,
            string name,
            string data)
        {
            var operation = Telemetry.StartOperation<DependencyTelemetry>(name);
            operation.Telemetry.Type = type;
            operation.Telemetry.Target = target;
            operation.Telemetry.Name = name;
            operation.Telemetry.Data = data;

            return operation;
        }

        internal static void SubmitException(IOperationHolder<DependencyTelemetry> telemtryOperation, Exception e)
        {
            telemtryOperation.Telemetry.Success = false;
            Telemetry.TrackException(e);
        }

        internal static void SubmitOperationResult(IOperationHolder<DependencyTelemetry> telemtryOperation)
        {
            Telemetry.StopOperation(telemtryOperation);
        }
    }
}
